// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Internal.Pooled;
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
                                .Debug($"[{nameof(TcpListenerBase)}] Closing {args.Connection.RemoteEndPoint}");

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
                                    .Debug($"[{nameof(TcpListenerBase)}] New connection from {connection.RemoteEndPoint}");
            this._protocol.OnAccept(connection);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] Process error from {connection.RemoteEndPoint}: {ex.Message}");
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
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] Accept error on {Config.Port}: {ex.Message}");

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

            if (!_listener.AcceptAsync(context.Args))
            {
                socket = context.Args.AcceptSocket!;

                if (!_connectionLimiter.IsConnectionAllowed(
                    ((System.Net.IPEndPoint)socket.RemoteEndPoint!).Address))
                {
                    SafeCloseSocket(socket);
                    throw new System.OperationCanceledException();
                }

                return context.Args.SocketError != System.Net.Sockets.SocketError.Success
                    ? throw new System.Net.Sockets.SocketException((System.Int32)context.Args.SocketError)
                    : InitializeConnection(socket, context);
            }

            // Wait async accept:
            socket = await context.PrepareAsync()
                                  .ConfigureAwait(false);

            if (!this._connectionLimiter.IsConnectionAllowed(
                ((System.Net.IPEndPoint)socket.RemoteEndPoint!).Address))
            {
                SafeCloseSocket(socket);
                throw new System.OperationCanceledException();
            }

            return this.InitializeConnection(socket, context);
        }
        catch
        {
            // Don't forget to return to pool in case of failure
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<PooledAcceptContext>(context);
            throw;
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
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return((PooledSocketAsyncEventArgs)e);
        }

        PooledAcceptContext context = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                              .Get<PooledAcceptContext>();

        PooledSocketAsyncEventArgs newArgs = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                     .Get<PooledSocketAsyncEventArgs>();

        newArgs.Context = context;
        context.Args = newArgs;

        newArgs.Completed += this.OnSyncAcceptCompleted;

        this.AcceptNext(newArgs);
    }

    [System.Diagnostics.DebuggerStepThrough]
    private void AcceptNext(System.Net.Sockets.SocketAsyncEventArgs args)
    {
        while (!_cancellationToken.IsCancellationRequested &&
                _listener?.IsBound == true)
        {
            try
            {
                if (_listener.IsBound)
                {
                    // Try accepting the connection asynchronously
                    if (_listener.AcceptAsync(args))
                    {
                        break;
                    }

                    // If the connection has been received synchronously, process it immediately.
                    this.HandleAccept(args);
                }
                else
                {
                    break;
                }
            }
            catch (System.Net.Sockets.SocketException ex) when (
                ex.SocketErrorCode is System.Net.Sockets.SocketError.Interrupted or
                System.Net.Sockets.SocketError.ConnectionAborted)
            {
                break;
            }
            catch (System.ObjectDisposedException)
            {
                break;
            }
            catch (System.Exception ex) when (!_cancellationToken.IsCancellationRequested)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] Accept error on {Config.Port}: {ex.Message}");

                // Brief delay to prevent CPU spinning on repeated errors
                _ = System.Threading.Tasks.Task.Delay(100, _cancellationToken)
                                               .ConfigureAwait(false);
            }
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    private void HandleAccept(System.Net.Sockets.SocketAsyncEventArgs e)
    {
        if (e.SocketError == System.Net.Sockets.SocketError.Success &&
            e.AcceptSocket is System.Net.Sockets.Socket socket)
        {
            try
            {
                if (!socket.Connected || socket.Handle.ToInt64() == -1)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[{nameof(TcpListenerBase)}] Socket is invalid or disconnected");

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
            }
            catch (System.ObjectDisposedException)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(TcpListenerBase)}] Socket was disposed during accept");

                SafeCloseSocket(socket);
                if (e is PooledSocketAsyncEventArgs pooled && pooled.Context != null)
                {
                    InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                            .Return<PooledAcceptContext>(pooled.Context);
                }
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] Process accept error: {ex.Message}");

                try
                {
                    socket.Close();
                }
                catch { }
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return<PooledAcceptContext>(((PooledSocketAsyncEventArgs)e).Context!);
            }
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(TcpListenerBase)}] Accept failed: {e.SocketError}");

            if (e is PooledSocketAsyncEventArgs pooled)
            {
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return<PooledAcceptContext>(pooled.Context!); // 💥 TH AcceptSocket == null
            }
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
                                    .Debug($"[{nameof(TcpListenerBase)}] ERROR closing socket: {ex.Message}");
        }
    }
}