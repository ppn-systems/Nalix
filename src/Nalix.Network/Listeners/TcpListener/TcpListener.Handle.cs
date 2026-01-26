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
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Timekeeping;

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
            _protocol.OnAccept(connection, _cancellationToken);

            this.Metrics.RECORD_ACCEPTED();
            s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] new={connection?.NetworkEndpoint}");
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] process-error={connection?.NetworkEndpoint}", ex);

            ArgumentNullException.ThrowIfNull(connection);
            connection.Close();
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

        args.Connection.OnProcessEvent -= _protocol.ProcessMessage;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage;

        args.Connection.Dispose();

        s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleConnectionClose)}] close={args.Connection?.NetworkEndpoint}");
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
        InitializeOptions(socket);

        // Trả context ngay tại đây, trước khi tạo connection
        // Context chỉ cần cho việc Accept, không cần sau đó
        s_pool.Return(context);

        IConnection connection = new Connection(socket);

        connection.OnCloseEvent += this.HandleConnectionClose;
        connection.OnCloseEvent += _limiter.OnConnectionClosed;

        connection.OnProcessEvent += _protocol.ProcessMessage;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage;

        if (s_config.EnableTimeout)
        {
            InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                    .Register(connection);
        }

        return connection;
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
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected static void SafeCloseSocket(Socket socket)
    {
        try
        {
            socket?.Close();
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(TcpListenerBase)}:Internal] accept-error ex={ex.Message}");
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
    /// On success the method validates the accepted socket, checks the connection limiter,
    /// wires up a <see cref="PooledListenerProcessContext"/>, calls
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
            if (args.SocketError == SocketError.Success &&
                args.AcceptSocket is Socket socket)
            {
                try
                {
                    if (!socket.Connected || socket.Handle.ToInt64() == -1)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] invalid-socket remote={socket.RemoteEndPoint}");

                        SafeCloseSocket(socket);
                        return;
                    }

                    if (socket.RemoteEndPoint is not IPEndPoint remoteIp ||
                        !_limiter.IsConnectionAllowed(remoteIp))
                    {
                        SafeCloseSocket(socket);
                        throw new NetworkException();
                    }

                    // Create and process connection similar to async version
                    PooledAcceptContext? context = ((PooledSocketAsyncEventArgs)args).Context ?? throw new InvalidOperationException("Accept context was not bound to pooled socket args.");
                    IConnection connection = this.InitializeConnection(socket, context);

                    // Process the connection
                    PooledListenerProcessContext ctx = s_pool.Get<PooledListenerProcessContext>();

                    ctx.Listener = this;
                    ctx.Connection = connection;

                    this.DISPATCH_CONNECTION(connection);

                    // Rebind a fresh context for the next accept on this args
                    PooledAcceptContext nextCtx = s_pool.Get<PooledAcceptContext>();

                    ((PooledSocketAsyncEventArgs)args).Context = nextCtx;
                    nextCtx.BindArgsForSync((PooledSocketAsyncEventArgs)args);
                }
                catch (ObjectDisposedException)
                {
                    s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] disposed-during-accept remote={socket.RemoteEndPoint}");

                    SafeCloseSocket(socket);
                    if (args is PooledSocketAsyncEventArgs pooled && pooled.Context != null)
                    {
                        s_pool.Return(pooled.Context);

                        // Rebind a fresh context for next accepts on this args
                        PooledAcceptContext newCtx = s_pool.Get<PooledAcceptContext>();

                        pooled.Context = newCtx;
                        newCtx.BindArgsForSync(pooled);
                    }
                }
                catch (Exception ex)
                {
                    this.Metrics.RECORD_ERROR();
                    s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-error port={_port}", ex);

                    SafeCloseSocket(socket);

                    if (((PooledSocketAsyncEventArgs)args).Context is PooledAcceptContext failedContext)
                    {
                        s_pool.Return(failedContext);
                    }

                    PooledAcceptContext newCtx = s_pool.Get<PooledAcceptContext>();

                    ((PooledSocketAsyncEventArgs)args).Context = newCtx;
                    newCtx.BindArgsForSync((PooledSocketAsyncEventArgs)args);
                }
            }
            else
            {
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-failed={args.SocketError}");

                if (args is PooledSocketAsyncEventArgs pooled)
                {
                    if (pooled.Context is not null)
                    {
                        s_pool.Return(pooled.Context);
                    }

                    pooled.Context = s_pool.Get<PooledAcceptContext>();

                    pooled.Context.BindArgsForSync(pooled);
                }
            }
        }
        finally
        {
            // Always clear to make the args reusable safely
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

        try
        {
            this.HandleAccept(args);
        }
        finally
        {
            // Unsubscribe before returning to pool to prevent duplicate callbacks
            args.Completed -= this.OnSyncAcceptCompleted;

            // Ensure the args is clean before returning to pool
            args.AcceptSocket = null;
            s_pool.Return((PooledSocketAsyncEventArgs)args);
        }

        PooledAcceptContext context = s_pool.Get<PooledAcceptContext>();
        PooledSocketAsyncEventArgs newArgs = s_pool.Get<PooledSocketAsyncEventArgs>();

        newArgs.Context = context;
        context.BindArgsForSync(newArgs);
        newArgs.Completed += this.OnSyncAcceptCompleted;

        this.AcceptNext(newArgs, _cancellationToken);
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
            Socket? s = Volatile.Read(ref _listener);
            if (s is null || !s.IsBound)
            {
                break;
            }

            // Re-arm args before each use
            args.AcceptSocket = null;

            try
            {
                // Async path: will continue in Completed handler
                if (s.AcceptAsync(args))
                {
                    break;
                }

                // Sync completion
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
                s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptNext)}] accept-error port={_port}", ex);

                // Brief delay to prevent CPU spinning on repeated errors
                Task.Delay(50, CancellationToken.None)
                                           .GetAwaiter()
                                           .GetResult();
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
    /// On each successfully accepted connection a <see cref="PooledListenerProcessContext"/>
    /// is retrieved from the pool, populated, and forwarded to <see cref="DISPATCH_CONNECTION"/>.
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
            ctx.Beat();

            IConnection connection;
            try
            {
                connection = await this.CreateConnectionAsync(cancellationToken)
                                       .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] shutdown-requested port={_port}");
                break;
            }
            catch (NetworkException)
            {
                if (cancellationToken.IsCancellationRequested || this.State != ListenerState.RUNNING)
                {
                    break;
                }

                // Rate-limited / rejected connection — tiếp tục
                await Task.Delay(10, CancellationToken.None)
                                                 .ConfigureAwait(false);
                continue;
            }
            catch (SocketException ex)
                when (IsIgnorableAcceptError(ex.SocketErrorCode, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested || this.State != ListenerState.RUNNING)
                {
                    break;
                }

                this.Metrics.RECORD_ERROR();
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] transient-socket-error={ex.SocketErrorCode} port={_port}");

                await Task.Delay(50, CancellationToken.None)
                                                 .ConfigureAwait(false);
                continue;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                this.Metrics.RECORD_ERROR();
                s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] accept-error port={_port}", ex);

                await Task.Delay(50, cancellationToken)
                                                 .ConfigureAwait(false);
                continue;
            }

            s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] accepted remote={connection.NetworkEndpoint} port={_port}");

            PooledListenerProcessContext pctx = s_pool.Get<PooledListenerProcessContext>();
            pctx.Listener = this;
            pctx.Connection = connection;

            this.DISPATCH_CONNECTION(connection);
            ctx.Advance(1, note: "accepted");
        }

        s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] loop-exited port={_port}");
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
    /// <exception cref="InvalidOperationException">
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

        bool contextReturned = false;
        PooledAcceptContext context = s_pool.Get<PooledAcceptContext>();

        try
        {
            Socket socket;

            if (_listener == null)
            {
                throw new InvalidOperationException("Socket is not initialized.");
            }

            // Wait async accept:
            socket = await context.BeginAcceptAsync(_listener, cancellationToken)
                                  .ConfigureAwait(false);

            EndPoint? remoteEndPoint = socket.RemoteEndPoint;
            if (remoteEndPoint is null || !_limiter.IsConnectionAllowed(remoteEndPoint))
            {
                SafeCloseSocket(socket);

                // Trả context ở đây vì InitializeConnection sẽ không được gọi
                contextReturned = true;
                s_pool.Return(context);

                this.Metrics.RECORD_REJECTED();
                throw new NetworkException($"Connection rejected: {remoteEndPoint}");
            }

            return this.InitializeConnection(socket, context);
        }
        catch (SocketException ex)
        {
            if (!contextReturned)
            {
                s_pool.Return(context);
            }

            throw new NetworkException($"Socket error while accepting. Code={ex.SocketErrorCode}", ex);
        }
        catch (OperationCanceledException)
        {
            if (!contextReturned)
            {
                s_pool.Return(context);
            }

            throw;
        }
        catch (NetworkException ex)
        {
            throw new NetworkException("Internal rejection during accept", ex);
        }
        catch (Exception ex)
        {
            if (!contextReturned)
            {
                s_pool.Return(context);
            }

            string remote = "unknown";

            try
            {
                remote = _listener?.LocalEndPoint?.ToString() ?? "null";
            }
            catch { }

            throw new NetworkException($"Accept failed. Listener={remote}, ContextReturned={contextReturned}", ex);
        }
    }
}
