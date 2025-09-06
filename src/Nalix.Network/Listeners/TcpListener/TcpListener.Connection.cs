// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Timing;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    #region Fields

    private static readonly System.Threading.WaitCallback ProcessConnectionCallback = static state =>
    {
        if (state is (TcpListenerBase listener, IConnection conn))
        {
            listener.ProcessConnection(conn);
        }
        else
        {
            throw new System.InvalidCastException(
                $"Invalid state object. " +
                $"Expected a (TcpListenerBase, IConnection), but received {state?.GetType().Name ?? "null"}.");

        }
    };

    #endregion Fields

    #region Internal

    internal sealed class NonFatalRejectedException : System.Exception { public NonFatalRejectedException() : base() { } }

    #endregion Internal

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IConnection InitializeConnection(
        System.Net.Sockets.Socket socket,
        PooledAcceptContext context)
    {
        ConfigureHighPerformanceSocket(socket);

        try
        {
            IConnection connection = new Connection.Connection(socket);

            connection.EnforceLimiterOnClose(_connectionLimiter);
            connection.OnCloseEvent += this.HandleConnectionClose;
            connection.OnProcessEvent += _protocol.ProcessMessage!;
            connection.OnPostProcessEvent += _protocol.PostProcessMessage!;

            if (Config.TimeoutOnConnect)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Register(connection);
            }

            return connection;
        }
        finally
        {
            // Ensure the context is returned to the pool even if connection creation fails
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<PooledAcceptContext>(context);
        }
    }

    /// <summary>
    /// Handles the closure of a connection by unsubscribing from its events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void HandleConnectionClose(System.Object? sender, IConnectEventArgs args)
    {
        if (args?.Connection == null)
        {
            return;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}] close={args.Connection.RemoteEndPoint}");

        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= this.HandleConnectionClose;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage!;

        args.Connection.Dispose();
    }

    /// <summary>
    /// Processes a new connection using the protocol handler.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ProcessConnection(IConnection connection)
    {
        try
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TcpListenerBase)}] new={connection.RemoteEndPoint}");

            this._protocol.OnAccept(connection, _cancellationToken);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] process-error={connection.RemoteEndPoint} ex={ex.Message}");
            connection.Close();
        }
    }

    /// <summary>
    /// Accepts connections in a loop until cancellation is requested
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    private async System.Threading.Tasks.Task AcceptConnectionsAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IConnection connection = await this
                    .CreateConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);

                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
                    ProcessConnectionCallback, (this, connection));
            }
            catch (NonFatalRejectedException)
            {
                await System.Threading.Tasks.Task.Delay(10, System.Threading.CancellationToken.None).ConfigureAwait(false);
                continue;
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (System.Net.Sockets.SocketException ex) when (IsIgnorableAcceptError(ex.SocketErrorCode, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested || State != ListenerState.Running)
                {
                    break;
                }

                // Transient: Gentle backoff to avoid spam
                await System.Threading.Tasks.Task.Delay(50, System.Threading.CancellationToken.None).ConfigureAwait(false);
                continue;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] accept-error ex={ex.Message}");

                // Brief delay to prevent CPU spinning on repeated errors
                await System.Threading.Tasks.Task.Delay(50, cancellationToken)
                                                 .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates a new connection from an incoming socket.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the connection creation process.</param>
    /// <returns>A task representing the connection creation.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private async System.Threading.Tasks.ValueTask<IConnection> CreateConnectionAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        PooledAcceptContext context = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                              .Get<PooledAcceptContext>();

        try
        {
            System.Net.Sockets.Socket socket;

            if (_listener == null)
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return<PooledAcceptContext>(context);

                throw new System.InvalidOperationException($"[{nameof(TcpListenerBase)}] socket is not initialized.");
            }

            // Wait async accept:
            socket = await context.BeginAcceptAsync(_listener).ConfigureAwait(false);

            if (!this._connectionLimiter.IsConnectionAllowed(
                ((System.Net.IPEndPoint)socket.RemoteEndPoint!).Address))
            {
                SafeCloseSocket(socket);

                throw new NonFatalRejectedException();
            }

            return this.InitializeConnection(socket, context);
        }
        catch
        {
            // Don't forget to return to pool in case of failure
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<PooledAcceptContext>(context);

            throw new NonFatalRejectedException();
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnSyncAcceptCompleted(
        System.Object? sender,
        System.Net.Sockets.SocketAsyncEventArgs e)
    {
        try
        {
            this.HandleAccept(e);
        }
        finally
        {
            // Unsubscribe before returning to pool to prevent duplicate callbacks
            e.Completed -= this.OnSyncAcceptCompleted;

            // Ensure the args is clean before returning to pool
            e.AcceptSocket = null;
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return((PooledSocketAsyncEventArgs)e);
        }

        PooledAcceptContext context = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                              .Get<PooledAcceptContext>();

        PooledSocketAsyncEventArgs newArgs = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                     .Get<PooledSocketAsyncEventArgs>();

        newArgs.Context = context;
        context.BindArgsForSync(newArgs);

        newArgs.Completed += this.OnSyncAcceptCompleted;

        this.AcceptNext(newArgs, _cancellationToken);
    }

    [System.Diagnostics.DebuggerStepThrough]
    private void AcceptNext(
        System.Net.Sockets.SocketAsyncEventArgs args,
        System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // Take a stable local copy to reduce races
            System.Net.Sockets.Socket? s = System.Threading.Volatile.Read(ref _listener);
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
                HandleAccept(args);
            }
            catch (System.ObjectDisposedException)
            {
                // Listener closed during/just before AcceptAsync
                break;
            }
            catch (System.Net.Sockets.SocketException ex) when (
                ex.SocketErrorCode is
                System.Net.Sockets.SocketError.Interrupted or
                System.Net.Sockets.SocketError.OperationAborted or
                System.Net.Sockets.SocketError.ConnectionAborted)
            {
                // Expected during shutdown
                break;
            }
            catch (System.Exception ex) when (!token.IsCancellationRequested)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] accept-error ex={ex.Message}");

                // Brief delay to prevent CPU spinning on repeated errors
                System.Threading.Tasks.Task.Delay(50, System.Threading.CancellationToken.None)
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

    [System.Diagnostics.DebuggerStepThrough]
    private void HandleAccept(System.Net.Sockets.SocketAsyncEventArgs e)
    {
        try
        {
            if (e.SocketError == System.Net.Sockets.SocketError.Success &&
           e.AcceptSocket is System.Net.Sockets.Socket socket)
            {
                try
                {
                    if (!socket.Connected || socket.Handle.ToInt64() == -1)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[{nameof(TcpListenerBase)}] invalid-socket remote={socket.RemoteEndPoint}");

                        SafeCloseSocket(socket);
                        return;
                    }

                    if (!_connectionLimiter.IsConnectionAllowed(
                        ((System.Net.IPEndPoint)socket.RemoteEndPoint!).Address))
                    {
                        SafeCloseSocket(socket);
                        return;
                    }

                    // Create and process connection similar to async version
                    PooledAcceptContext context = ((PooledSocketAsyncEventArgs)e).Context!;
                    IConnection connection = this.InitializeConnection(socket, context);

                    // Process the connection
                    _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
                        ProcessConnectionCallback, (this, connection));

                    // Rebind a fresh context for the next accept on this args
                    PooledAcceptContext nextCtx = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                          .Get<PooledAcceptContext>();
                    ((PooledSocketAsyncEventArgs)e).Context = nextCtx;
                    nextCtx.BindArgsForSync((PooledSocketAsyncEventArgs)e);
                }
                catch (System.ObjectDisposedException)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[{nameof(TcpListenerBase)}] disposed-during-accept remote={socket.RemoteEndPoint}");

                    SafeCloseSocket(socket);
                    if (e is PooledSocketAsyncEventArgs pooled && pooled.Context != null)
                    {
                        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                .Return<PooledAcceptContext>(pooled.Context);

                        // Rebind a fresh context for next accepts on this args
                        PooledAcceptContext newCtx = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                             .Get<PooledAcceptContext>();
                        pooled.Context = newCtx;
                        newCtx.BindArgsForSync(pooled);
                    }
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(TcpListenerBase)}] accept-error ex={ex.Message}");

                    try
                    {
                        socket.Close();
                    }
                    catch { }
                    InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                            .Return<PooledAcceptContext>(((PooledSocketAsyncEventArgs)e).Context!);

                    PooledAcceptContext newCtx = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                         .Get<PooledAcceptContext>();
                    ((PooledSocketAsyncEventArgs)e).Context = newCtx;
                    newCtx.BindArgsForSync((PooledSocketAsyncEventArgs)e);
                }
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(TcpListenerBase)}] accept-failed={e.SocketError}");

                if (e is PooledSocketAsyncEventArgs pooled)
                {
                    ObjectPoolManager pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
                    PooledAcceptContext? oldCtx = pooled.Context;
                    if (oldCtx is not null)
                    {
                        pool.Return<PooledAcceptContext>(oldCtx);
                    }

                    PooledAcceptContext newCtx = pool.Get<PooledAcceptContext>();
                    pooled.Context = newCtx;
                    newCtx.BindArgsForSync(pooled);
                }
            }
        }
        finally
        {
            // Always clear to make the args reusable safely
            e.AcceptSocket = null;
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SafeCloseSocket(System.Net.Sockets.Socket socket)
    {
        try
        {
            socket?.Close();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TcpListenerBase)}] accept-error ex={ex.Message}");
        }
    }
}