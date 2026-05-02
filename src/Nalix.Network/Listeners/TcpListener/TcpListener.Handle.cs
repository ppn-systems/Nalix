// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Codec.Transforms;
using Nalix.Network.Connections;
using Nalix.Network.Internal;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Internal.Time;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    /// <summary>
    /// Finalizes the acceptance of an incoming connection by invoking the protocol handler
    /// and recording the accepted metric.
    /// </summary>
    /// <param name="connection">
    /// The fully initialized <see cref="IConnection"/> instance representing the accepted client.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// If the protocol's <c>OnAccept</c> call throws, the exception is caught, logged, and the
    /// connection is closed immediately — the listener loop continues uninterrupted.
    /// </remarks>
    [DebuggerStepThrough]
    protected void ProcessConnection(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            this.DoAccept(connection);
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] " +
                    $"new={connection.NetworkEndpoint}");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] " +
                    $"process-error={connection.NetworkEndpoint}");
            }

            // Disconnect the connection immediately if an error occurs -> prevent resource leaks.
            // WHY Disconnect here: If OnAccept throws an error, the connection has not been registered to any
            // managed list -> you must close it manually here; no one else can do it.
            connection.Dispose();
        }
    }

    /// <summary>
    /// Processes an incoming network frame from a connected client.
    /// Applies inbound pipeline transformations (e.g., decrypt, decompress),
    /// optionally replaces the underlying buffer lease, then forwards the
    /// processed message to the protocol layer for handling.
    /// </summary>
    /// <param name="sender">The source of the event triggering this frame processing.</param>
    /// <param name="args">Connection event arguments containing the frame data and connection context.</param>
    /// <remarks>
    /// This method is performance-critical and is intentionally marked with <see cref="DebuggerStepThroughAttribute"/>
    /// to avoid stepping into during debugging sessions.
    ///
    /// Pipeline behavior:
    /// <list type="number">
    /// <item>Validates and extracts the buffer lease from event args.</item>
    /// <item>Applies inbound transformations via <c>FramePipeline.ProcessInbound</c>.</item>
    /// <item>Replaces the lease if pipeline produces a new buffer.</item>
    /// <item>Forwards the event to protocol handler.</item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when lease is missing from event args.</exception>
    /// <exception cref="CipherException">May occur during cryptographic processing.</exception>
    /// <exception cref="InvalidCastException">May occur during frame decoding.</exception>
    /// <exception cref="SerializationFailureException">Thrown when deserialization fails.</exception>
    /// <exception cref="Exception">Unhandled exceptions are logged and reported to connection error handler.</exception>
    [DebuggerStepThrough]
    protected void ProcessFrame(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args is not ConnectionEventArgs replaceable)
        {
            return;
        }

        IBufferLease lease = args.Lease ?? throw new InvalidOperationException("Event args must have Lease.");
        IBufferLease current = lease;
        bool exchanged = false;

        try
        {
            FramePipeline.ProcessInbound(ref current, args.Connection.Secret.AsSpan(), args.Connection.Algorithm);

            if (!ReferenceEquals(current, lease))
            {
                replaceable.ExchangeLease(current)?.Dispose();
                lease = current;
                exchanged = true;
            }

            _protocol.ProcessMessage(sender, args);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (ex is CipherException or InternalErrorException or SerializationFailureException or LZ4Exception)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessFrame)}] {ex.Message}");
                }
            }
            else
            {
                args.Connection.ThrottledError(
                    _logger, "protocol.process_error",
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessFrame)}] Unhandled exception during message processing.", ex);
            }
        }
        finally
        {
            if (!exchanged && !ReferenceEquals(current, lease))
            {
                current.Dispose();
            }
        }
    }

    /// <summary>
    /// Handles the <see cref="IConnection.OnCloseEvent"/> event raised when a client connection
    /// is closed, either by the remote peer or by the server.
    /// </summary>
    /// <param name="sender">
    /// The object that raised the event. May be <see langword="null"/>.
    /// </param>
    /// <param name="args">
    /// The event arguments containing the <see cref="IConnection"/> that was closed.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// Unsubscribes all event handlers from the closing connection before calling
    /// <see cref="Connection.Dispose"/> to prevent memory leaks and duplicate callbacks.
    /// </para>
    /// <para>
    /// If <paramref name="args"/> or its <see cref="IConnectEventArgs.Connection"/> is
    /// <see langword="null"/>, this method returns immediately without performing any action.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    protected void HandleConnectionClose(object? sender, IConnectEventArgs args)
    {
        if (args?.Connection == null)
        {
            return;
        }

        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= this.HandleConnectionClose;
        args.Connection.OnCloseEvent -= _limiter.OnConnectionClosed;

        // Keep unwiring post-process as before (if you subscribed it).
        args.Connection.OnProcessEvent -= this.ProcessFrame;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage;

        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                $"[NW.{nameof(TcpListenerBase)}:{nameof(HandleConnectionClose)}] " +
                $"close={args.Connection.NetworkEndpoint}");
        }

        args.Connection.Dispose();
    }

    /// <summary>
    /// Configures socket options, constructs a new <see cref="IConnection"/>, and subscribes
    /// all required event handlers on it.
    /// </summary>
    /// <param name="socket">
    /// The raw <see cref="Socket"/> accepted from the listener.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <param name="context">
    /// The pooled accept context that was used for the accept operation.
    /// Returned to the pool immediately after this method claims the socket — callers must
    /// not touch <paramref name="context"/> after this call returns.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A fully configured <see cref="IConnection"/> ready to send and receive data.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The <paramref name="context"/> is returned to the pool <em>before</em> the connection
    /// object is constructed, because the context is only needed during the accept phase and
    /// the pool slot can be reused immediately by the next pending accept.
    /// </para>
    /// <para>
    /// If <c>EnableTimeout</c> is set in the server configuration, the connection is registered
    /// with the <see cref="TimingWheel"/> for idle-timeout tracking.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    private IConnection InitializeConnection(Socket socket, PooledAcceptContext context)
    {
        // Order of importance:
        // 1. Configure socket OPTIONS first (it may throw if the socket is invalid).
        // 2. Return the context to the pool (only after step 1 is successful).
        // 3. Create a connection wrapper and subscribe events.
        //
        // WHY doesn't try/finally be used to return the context?
        // If InitializeOptions throw -> caller (ProcessAcceptedSocket/CreateConnectionAsync)
        // It still holds the reference socket and will close it itself in its catch block.
        // If the return context is here when throw -> caller doesn't know the context has been returned ->
        // double-return bug: context returned twice -> pool corrupted.
        this.InitializeOptions(socket);

        // Context only needs to return immediately during the accept — phase after the socket has been claimed.
        // This pool slot will be reused for the next accept without waiting.
        _pool.Return(context);

        Connection connection = new(socket, _logger);
        try
        {
            // Subscribe lifecycle events.
            // WHY subscribe to _limiter.OnConnectionClosed:
            //      When connection close -> limit, the counter must be reduced -> allow a new connection.
            connection.OnCloseEvent += this.HandleConnectionClose;
            connection.OnCloseEvent += _limiter.OnConnectionClosed;

            // Wire the internal listener method to handle the shared pipeline before routing.
            connection.OnProcessEvent += this.ProcessFrame;

            // Keep post-process as you already have (optional).
            // If your PostProcessMessage should run after app protocol, leaving it subscribed is OK
            // as long as it depends on the same args lifecycle rules.
            connection.OnPostProcessEvent += _protocol.PostProcessMessage;

            if (_config.EnableTimeout)
            {
                // Register connection with TimingWheel to track idle timeout.
                // WHY TimingWheel instead of Timer per-connection:
                // - Timer per-connection: O(n) memory + GC pressure when there are thousands of connections.
                // - TimingWheel: O(1) tick, shared wheel for all connections -> much more efficient.
                _timing.Register(connection);
            }

            return connection;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Closes a socket, swallowing any exception that occurs during the close operation.
    /// </summary>
    /// <param name="socket">
    /// The socket to close. If <see langword="null"/>, this method is a no-op.
    /// </param>
    /// <remarks>
    /// Intended for use in error-recovery paths where the socket may already be in an
    /// indeterminate state. Exceptions are logged at <c>Debug</c> level and not rethrown,
    /// so the caller's error-handling flow is never interrupted by a secondary failure.
    /// </remarks>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    protected void SafeCloseSocket(Socket socket)
    {
        try
        {
            // socket? -> null-conditional: does not throw NullReferenceException if null.
            socket?.Close();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                // Log in Trace (not Error) because this is expected failure in error-recovery path.
                // WHY not rethrow: Currently in cleanup path -> the second exception will obscure the original exception.
                _logger.LogTrace($"[NW.{nameof(TcpListenerBase)}:Internal] accept-error ex={ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processes the result of a single accept operation represented by
    /// <paramref name="args"/>, initializing the connection on success or recovering
    /// the pooled resources on failure.
    /// </summary>
    /// <param name="args">
    /// The <see cref="SocketAsyncEventArgs"/> that completed the accept.
    /// Must be a <see cref="PooledSocketAsyncEventArgs"/> instance.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// On success the method validates the accepted socket, checks the connection limiter, calls
    /// <see cref="DISPATCH_CONNECTION"/>, and rebinds a fresh <see cref="PooledAcceptContext"/>
    /// on <paramref name="args"/> so it can be reused for the next accept.
    /// </para>
    /// <para>
    /// On failure the method always ensures that any borrowed pool objects are returned and
    /// that <see cref="SocketAsyncEventArgs.AcceptSocket"/> is reset to
    /// <see langword="null"/> (in the <c>finally</c> block) so the args is safe to reuse.
    /// </para>
    /// <para>
    /// Three distinct exception paths are handled:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="ObjectDisposedException"/> — the listener was closed mid-accept;
    ///     logged as a warning, socket and context are cleaned up.
    ///   </item>
    ///   <item>
    ///     <see cref="Exception"/> (general) — metrics are incremented, error is logged,
    ///     socket and context are cleaned up, and a fresh context is bound for the next accept.
    ///   </item>
    ///   <item>
    ///     <see cref="SocketError"/> != <c>Success</c> — accept did not
    ///     produce a socket; context is returned and rebound.
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="NetworkException"></exception>
    [DebuggerStepThrough]
    protected void HandleAccept(SocketAsyncEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            if (args.SocketError != SocketError.Success)
            {
                // SocketError check first — cheapest path, no pattern match required.
                // This is an early exit for all OS-level errors (Interrupted, OperationAborted, etc.)
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-failed={args.SocketError}");
                }

                this.RebindAcceptContext((PooledSocketAsyncEventArgs)args);
                return;
            }

            if (args.AcceptSocket is not Socket socket)
            {
                // SocketError == Success but AcceptSocket null — a rare case,
                // usually due to a race between Close() and Completed callbacks.
                // No socket to log endpoint, logging warning is sufficient.
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-socket-null port={_port}");
                }

                this.RebindAcceptContext((PooledSocketAsyncEventArgs)args);
                return;
            }

            IConnection? connection = null;
            try
            {
                // Create and process connection similar to async version
                PooledAcceptContext? context = ((PooledSocketAsyncEventArgs)args).Context
                    ?? throw new InternalErrorException("TryAccept context was not bound to pooled socket args.");

                if (this.IsProcessChannelFull())
                {
                    this.Metrics.RECORD_REJECTED();
                    if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] channel-full port={_port} - dropped socket directly");
                    }
                    this.SafeCloseSocket(socket);
                    this.RebindAcceptContext((PooledSocketAsyncEventArgs)args);
                    return;
                }

#pragma warning disable CA2000
                connection = this.ProcessAcceptedSocket(socket, context);
#pragma warning restore CA2000

                // Process the connection
                this.DISPATCH_CONNECTION(connection);

                // Prepare args for the NEXT accept immediately.
                // WHY prepare now: AcceptNext will call AcceptAsync with this args.
                // Otherwise, rebind context -> the old context (returned to the pool) is reused -> bug.
                PooledAcceptContext nextCtx = _pool.Get<PooledAcceptContext>();

                ((PooledSocketAsyncEventArgs)args).Context = nextCtx;
                nextCtx.BindArgsForSync((PooledSocketAsyncEventArgs)args);
            }
            catch (ObjectDisposedException)
            {
                // Listener is disposed of while accept is running -> this is expected shutdown case.
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] disposed-during-accept remote={socket.RemoteEndPoint?.ToString() ?? "<null>"}");
                }

                if (connection != null)
                {
                    connection.Dispose();
                }
                else
                {
                    this.SafeCloseSocket(socket);
                }

                this.RebindAcceptContext((PooledSocketAsyncEventArgs)args);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                this.Metrics.RECORD_ERROR();
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, $"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-error port={_port}");
                }

                if (connection != null)
                {
                    connection.Dispose();
                }
                else
                {
                    this.SafeCloseSocket(socket);
                }

                this.RebindAcceptContext((PooledSocketAsyncEventArgs)args);
            }
        }
        finally
        {
            // ALWAYS clear AcceptSocket in finally.
            // WHY: SocketAsyncEventArgs is pooled and reused.
            // If AcceptSocket is not cleared -> the next time AcceptAsync is used, reject args(throw).
            // Finally ensures clear even if HandleAccept throw -> args always safe to reuse.
            args.AcceptSocket = null;
        }
    }

    /// <summary>
    /// Callback invoked by the socket runtime when a synchronous-path accept operation
    /// completes asynchronously (i.e. <see cref="Socket.AcceptAsync(SocketAsyncEventArgs)"/>
    /// returned <see langword="true"/> and later fired the <c>Completed</c> event).
    /// </summary>
    /// <param name="sender">
    /// The source of the event. May be <see langword="null"/>.
    /// </param>
    /// <param name="args">
    /// The <see cref="SocketAsyncEventArgs"/> whose accept operation
    /// completed. Must be a <see cref="PooledSocketAsyncEventArgs"/> instance.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// After processing the completed accept via <see cref="HandleAccept"/>, this method
    /// unsubscribes itself from <paramref name="args"/> and returns the args to the pool.
    /// It then allocates a fresh pair of <see cref="PooledAcceptContext"/> and
    /// <see cref="PooledSocketAsyncEventArgs"/>, wires up the callback, and calls
    /// <see cref="AcceptNext"/> to keep the accept pipeline flowing.
    /// </para>
    /// <para>
    /// The unsubscription happens in the <c>finally</c> block to guarantee it occurs even if
    /// <see cref="HandleAccept"/> throws, preventing the args from firing a stale callback
    /// after it has been returned to the pool.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    protected void OnSyncAcceptCompleted(object? sender, SocketAsyncEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Prepare NEW args BEFORE processing the current args.
        // WHY before: After HandleAccept + Return(args), the old args belong to the pool.
        // AcceptNext requires new args ready to call immediately -> cannot allocate after Return.
        PooledAcceptContext context = _pool.Get<PooledAcceptContext>();
        PooledSocketAsyncEventArgs newArgs = _pool.Get<PooledSocketAsyncEventArgs>();

        newArgs.Context = context;
        context.BindArgsForSync(newArgs);
        newArgs.Completed += this.OnSyncAcceptCompleted;

        try
        {
            this.HandleAccept(args);
        }
        finally
        {
            // Unsubscribe BEFORE returning args to the pool.
            // WHY: Otherwise, unsubscribe -> pool can return this args for another accept ->
            // When that accept is complete, the old callback will be called -> duplicate processing bug.
            args.Completed -= this.OnSyncAcceptCompleted;

            // Ensure the args is clean before returning to pool
            args.AcceptSocket = null;
            _pool.Return((PooledSocketAsyncEventArgs)args);

            // Continue the accept pipeline with the new args.
            // WHY in finally: Ensure the pipeline does not stop even if HandleAccept throws.
            // newArgs has been prepared beforehand try -> cannot fail because OOM is here.
            this.AcceptNext(newArgs, _cancellationToken);
        }
    }

    /// <summary>
    /// Drives the synchronous accept loop: calls
    /// <see cref="Socket.AcceptAsync(SocketAsyncEventArgs)"/> in a tight loop, handling
    /// both the immediate (synchronous) completion path and scheduling the
    /// asynchronous completion path via the <c>Completed</c> event.
    /// </summary>
    /// <param name="args">
    /// The <see cref="SocketAsyncEventArgs"/> to use for each accept call.
    /// Must be a <see cref="PooledSocketAsyncEventArgs"/> with a bound
    /// <see cref="PooledAcceptContext"/>.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to signal that the listener is shutting down.
    /// The loop exits cleanly when cancellation is requested.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// When <see cref="Socket.AcceptAsync(SocketAsyncEventArgs)"/> returns
    /// <see langword="true"/> the operation is pending — the loop breaks and control returns
    /// to the caller; the <c>Completed</c> event on <paramref name="args"/> will resume
    /// processing via <see cref="OnSyncAcceptCompleted"/>.
    /// </para>
    /// <para>
    /// When <see cref="Socket.AcceptAsync(SocketAsyncEventArgs)"/> returns
    /// <see langword="false"/> the accept completed synchronously — <see cref="HandleAccept"/>
    /// is called inline and the loop continues.
    /// </para>
    /// <para>
    /// Expected shutdown exceptions (<see cref="ObjectDisposedException"/>,
    /// <see cref="SocketError.Interrupted"/>,
    /// <see cref="SocketError.OperationAborted"/>,
    /// <see cref="SocketError.ConnectionAborted"/>) cause a clean break.
    /// Other exceptions are logged and the loop pauses for 50 ms before retrying to avoid
    /// CPU-spinning on persistent errors.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    protected void AcceptNext(SocketAsyncEventArgs args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        while (!cancellationToken.IsCancellationRequested)
        {
            // Take a stable local copy to reduce races
            Socket? s = _listener;
            if (s is null || !s.IsBound)
            {
                break;
            }

            // Re-arm args before each use
            args.AcceptSocket = null;

            try
            {
                // AcceptAsync(args) returns:
                // true -> operation pending (async) -> Completed event will fire later -> break loop.
                // false -> operation complete immediately (sync) -> call HandleAccept inline -> continue loop.
                if (s.AcceptAsync(args))
                {
                    // Async path: OnSyncAcceptCompleted will call AcceptNext next.
                    break;
                }

                // Sync completion: process directly within this thread -> no ThreadPool hop needed.
                this.HandleAccept(args);
            }
            catch (ObjectDisposedException)
            {
                // Listener closed during/just before AcceptAsync
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode is
                   SocketError.Interrupted or
                   SocketError.OperationAborted or
                   SocketError.ConnectionAborted)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, $"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptNext)}] accept-error port={_port}");
                }

                // Delay 50ms to avoid CPU spinning during persistent errors (eg, file descriptor explosion).
                // Use Thread.Sleep because this is a synchronous wait on a background worker thread.
                // Avoids allocating a Task object just to block.
                Thread.Sleep(50);
            }
            finally
            {
                // Ensure args reusable
                args.AcceptSocket = null;
            }
        }
    }

    /// <summary>
    /// Asynchronous accept loop that runs as a background worker for the lifetime of the listener.
    /// Continuously accepts incoming TCP connections and dispatches each one for processing.
    /// </summary>
    /// <param name="ctx">
    /// The worker context used to signal liveness (heartbeat) and to track processed-connection
    /// counts. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to signal a graceful shutdown. When cancelled, the loop exits after the
    /// current accept completes or is interrupted.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the loop has exited.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The following exception types are handled without terminating the loop:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="OperationCanceledException"/> (when <paramref name="cancellationToken"/>
    ///     is cancelled) — exits the loop cleanly.
    ///   </item>
    ///   <item>
    ///     <see cref="NetworkException"/> — a rate-limited or limiter-rejected connection;
    ///     the loop pauses for 10 ms and continues.
    ///   </item>
    ///   <item>
    ///     <see cref="SocketException"/> with an ignorable error code
    ///     (see <c>IsIgnorableAcceptError</c>) — transient OS-level accept failure; the loop
    ///     pauses for 50 ms and continues.
    ///   </item>
    ///   <item>
    ///     Any other <see cref="Exception"/> (when not cancelled) — unexpected failure;
    ///     metrics are incremented, the error is logged, and the loop pauses for 50 ms.
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// On each successfully accepted connection forwarded to <see cref="DISPATCH_CONNECTION"/>.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected async Task AcceptConnectionsAsync(IWorkerContext ctx, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        TimeSpan heartbeatInterval = TimeSpan.FromSeconds(2);

        while (!cancellationToken.IsCancellationRequested)
        {
            // ctx.Beat() = heartbeat signal tells TaskManager that the worker is still alive.
            // WHY requires heartbeat: If the worker is hanged (deadlock/infinite loop), TaskManager
            // It can detect and restart/alert based on heartbeat timeout.
            ctx.Beat();

            IConnection connection;
            try
            {
                connection = await this.CreateConnectionAsync(cancellationToken)
                                       .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
                {
                    // Token cancelled -> shutdown graceful -> exit loop.
                    _logger.LogTrace(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] " +
                        $"shutdown-requested port={_port}");
                }

                break;
            }
            catch (NetworkException)
            {
                if (cancellationToken.IsCancellationRequested || this.State != ListenerState.RUNNING)
                {
                    break;
                }

                // Rate-limited or limiter rejects -> short delay and then try again.
                // WHY 10ms: Enough for the limiter to "cool down" but not too long -> responsive.
                // WHY CancellationToken.None: This delay must be completed completely (without interruptions).
                await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);
                continue;
            }
            catch (SocketException ex) when (IsIgnorableAcceptError(ex.SocketErrorCode, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested || this.State != ListenerState.RUNNING)
                {
                    break;
                }

                this.Metrics.RECORD_ERROR();
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] transient-socket-error={ex.SocketErrorCode} port={_port}");
                }

                // Transient OS-level error -> record metric + delay + retry.
                // WHY 50ms: Longer than NetworkException delay because OS-level errors often require a recovery time.
                await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);

                continue;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                this.Metrics.RECORD_ERROR();
                if (_logger != null && _logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(
                        ex,
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] " +
                        $"accept-error port={_port}");
                }

                // Unexpected error -> record + log + delay 50ms to avoid CPU spin.
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] " +
                    $"accepted remote={connection.NetworkEndpoint} port={_port}");
            }

            // Send the connection to process channel -> consumer thread for processing.
            this.DISPATCH_CONNECTION(connection);
            ctx.Advance(1, note: "accepted");
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                $"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] " +
                $"loop-exited port={_port}");
        }
    }

    /// <summary>
    /// Asynchronously accepts a single TCP connection from the listener socket,
    /// validates it against the connection limiter, and returns a fully initialized
    /// <see cref="IConnection"/>.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to abort the accept operation. When cancelled,
    /// <see cref="OperationCanceledException"/> is propagated to the caller.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is the
    /// accepted and initialized <see cref="IConnection"/>.
    /// </returns>
    /// <exception cref="InternalErrorException">
    /// Thrown when the listener socket has not been initialized (i.e. <c>_listener</c> is
    /// <see langword="null"/>).
    /// </exception>
    /// <exception cref="NetworkException">
    /// Thrown in the following cases:
    /// <list type="bullet">
    ///   <item>The remote endpoint was rejected by the connection limiter.</item>
    ///   <item>A <see cref="SocketException"/> occurred during accept.</item>
    ///   <item>Any other unexpected exception occurred during accept.</item>
    /// </list>
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Propagated when <paramref name="cancellationToken"/> is cancelled during the
    /// async accept wait.
    /// </exception>
    /// <remarks>
    /// <para>
    /// A <see cref="PooledAcceptContext"/> is borrowed from the pool before the async accept
    /// and is returned to the pool in all exit paths — either by
    /// <see cref="InitializeConnection"/> on the success path, or explicitly in every
    /// catch/early-return branch via a <c>contextReturned</c> guard flag to avoid
    /// double-return bugs.
    /// </para>
    /// <para>
    /// The <c>cancellationToken</c> parameter is intentionally unused beyond the initial
    /// <see cref="CancellationToken.ThrowIfCancellationRequested"/> check;
    /// the actual token is forwarded to <c>BeginAcceptAsync</c> internally.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    protected async ValueTask<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // contextOwned = true: context is the responsibility of this method (requires return to pool).
        // contextOwned = false: ownership has been transferred to InitializeConnection.
        // This pattern prevents double-return (returns twice to the pool) and memory leak(no return).
        bool contextOwned = true;
        PooledAcceptContext context = _pool.Get<PooledAcceptContext>();

        try
        {
            Socket socket;

            if (_listener == null)
            {
                throw new InternalErrorException("Socket is not initialized.");
            }

            // Wait async accept:
            socket = await context.BeginAcceptAsync(_listener, cancellationToken)
                                  .ConfigureAwait(false);

            if (this.IsProcessChannelFull())
            {
                this.SafeCloseSocket(socket);
                Throw.ProcessChannelFull();
            }

            // Validate and limit checks occur BEFORE ownership transfer.
            // If a throw occurs here (invalid socket, limiter reject), contextOwned remains true.
            // -> finally, Return(context) will be true.
            if (!socket.Connected || socket.Handle.ToInt64() == -1)
            {
                this.SafeCloseSocket(socket);
                Throw.InvalidSocket();
            }

            if (socket.RemoteEndPoint is not IPEndPoint ip || !_limiter.TryAccept(ip))
            {
                this.SafeCloseSocket(socket);
                Throw.ConnectionRejectedByLimiter();
            }

            // Transfer ownership: InitializeConnection will return the inner context.
            // Set contextOwned = false BEFORE calling so that if InitializeConnection throws an error, it will not double-return.
            // // After returning the context, it will not double-return.
            contextOwned = false;
            return this.InitializeConnection(socket, context);
        }
        catch (SocketException ex)
        {
            throw new NetworkException($"Socket error while accepting. Code={ex.SocketErrorCode}", ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (NetworkException)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            string remote = "unknown";
            try
            {
                remote = _listener?.LocalEndPoint?.ToString() ?? "<null>";
            }
            catch (ObjectDisposedException ode)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(CreateConnectionAsync)}] " +
                        $"listener-endpoint-disposed port={_port} reason={ode.GetType().Name}");
                }
            }
            catch (Exception lookupEx) when (ExceptionClassifier.IsNonFatal(lookupEx))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(lookupEx,
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(CreateConnectionAsync)}] " +
                        $"listener-endpoint-lookup-failed port={_port}");
                }
            }
            throw new NetworkException($"TryAccept failed. Listener={remote}", ex);
        }
        finally
        {
            // WHY: `finally` only returns when `contextOwned` = true.
            // If `contextOwned` = false: InitializeConnection has already returned.
            // If `contextOwned` = true: an exception occurred before the ownership transfer.
            // This pattern completely eliminates the possibility of double-returns and simultaneous leaks.
            if (contextOwned)
            {
                _pool.Return(context);
            }
        }
    }

    /// <inheritdoc/>
    /// <param name="connection">
    /// The fully initialized <see cref="IConnection"/> instance representing the accepted client.
    /// Must not be <see langword="null"/>.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void DoAccept(IConnection connection)
    {
        _protocol.OnAccept(connection, _cancellationToken);

        if (connection != null && !connection.IsDisposed)
        {
            _hub.RegisterConnection(connection);
            this.Metrics.RECORD_ACCEPTED();
        }
        else
        {
            this.Metrics.RECORD_REJECTED();
        }
    }

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RebindAcceptContext(PooledSocketAsyncEventArgs pooled)
    {
        if (pooled.Context is PooledAcceptContext ctx)
        {
            _pool.Return(ctx);
        }

        PooledAcceptContext next = _pool.Get<PooledAcceptContext>();
        pooled.Context = next;
        next.BindArgsForSync(pooled);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(socket))]
    private IConnection ProcessAcceptedSocket(Socket socket, PooledAcceptContext context)
    {
        // Validate and limit checks occur BEFORE ownership transfer.
        // If a throw occurs here (invalid socket, limiter reject), contextOwned remains true.
        // -> finally, Return(context) will be true.
        if (!socket.Connected || socket.Handle.ToInt64() == -1)
        {
            this.SafeCloseSocket(socket);
            Throw.InvalidSocket();
        }

        // Check the connection limiter before proceeding.
        // If the limiter rejects the connection, close the socket and throw to trigger the appropriate metrics and logging in the caller.
        if (socket.RemoteEndPoint is not IPEndPoint ip || !_limiter.TryAccept(ip))
        {
            this.SafeCloseSocket(socket);
            Throw.ConnectionRejectedByLimiter();
        }

        IConnection connection = this.InitializeConnection(socket, context);

        return connection;
    }

    #endregion Private Methods
}
