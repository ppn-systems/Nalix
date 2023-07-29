using Nalix.Common.Connection;
using Nalix.Network.Connection;
using Nalix.Network.Internal;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Listeners;

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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IConnection InitializeConnection(
        System.Net.Sockets.Socket socket,
        PooledAcceptContext context)
    {
        ConfigureHighPerformanceSocket(socket);

        try
        {
            IConnection connection = new Connection.Connection(socket, _bufferPool, _logger);

            connection.EnforceLimiterOnClose(_connectionLimiter);
            connection.OnCloseEvent += this.HandleConnectionClose;
            connection.OnProcessEvent += _protocol.ProcessMessage!;
            connection.OnPostProcessEvent += _protocol.PostProcessMessage!;

            return connection;
        }
        finally
        {
            // Ensure the context is returned to the pool even if connection creation fails
            ObjectPoolManager.Instance.Return<PooledAcceptContext>(context);
        }
    }

    /// <summary>
    /// Handles the closure of a connection by unsubscribing from its events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void HandleConnectionClose(System.Object? sender, IConnectEventArgs args)
    {
        if (args?.Connection == null)
        {
            return;
        }

        _logger.Debug("[TCP] Closing {0}", args.Connection.RemoteEndPoint);

        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= this.HandleConnectionClose;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage!;

        args.Connection.Dispose();
    }

    /// <summary>
    /// Processes a new connection using the protocol handler.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ProcessConnection(IConnection connection)
    {
        try
        {
            this._logger.Debug("[TCP] New connection from {0}", connection.RemoteEndPoint);
            this._protocol.OnAccept(connection);
        }
        catch (System.Exception ex)
        {
            this._logger.Error("[TCP] Process error from {0}: {1}", connection.RemoteEndPoint, ex.Message);
            connection.Close();
        }
    }

    /// <summary>
    /// Accepts connections in a loop until cancellation is requested
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
                _logger.Error("[TCP] Accept error on {0}: {1}", Config.Port, ex.Message);
                // Brief delay to prevent CPU spinning on repeated errors
                await System.Threading.Tasks.Task
                        .Delay(50, cancellationToken)
                        .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates a new connection from an incoming socket.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the connection creation process.</param>
    /// <returns>A task representing the connection creation.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private async System.Threading.Tasks.ValueTask<IConnection> CreateConnectionAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        PooledAcceptContext context = ObjectPoolManager.Instance.Get<PooledAcceptContext>();

        try
        {
            System.Net.Sockets.Socket socket;

            if (_listener == null)
            {
                ObjectPoolManager.Instance.Return<PooledAcceptContext>(context);
                throw new System.InvalidOperationException("TcpListenerBase socket is not initialized.");
            }

            if (!_listener.AcceptAsync(context.Args))
            {
                socket = context.Args.AcceptSocket!;

                if (!_connectionLimiter.IsConnectionAllowed(
                    ((System.Net.IPEndPoint)socket.RemoteEndPoint!).Address))
                {
                    this.SafeCloseSocket(socket);
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
                this.SafeCloseSocket(socket);
                throw new System.OperationCanceledException();
            }

            return this.InitializeConnection(socket, context);
        }
        catch
        {
            // Don't forget to return to pool in case of failure
            ObjectPoolManager.Instance.Return<PooledAcceptContext>(context);
            throw;
        }
    }

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
            ObjectPoolManager.Instance.Return((PooledSocketAsyncEventArgs)e);
        }

        PooledAcceptContext context = ObjectPoolManager.Instance.Get<PooledAcceptContext>();
        PooledSocketAsyncEventArgs newArgs = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();

        newArgs.Context = context;
        context.Args = newArgs;

        newArgs.Completed += this.OnSyncAcceptCompleted;

        this.AcceptNext(newArgs);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
                _logger.Error("[TCP] Accept error on {0}: {1}", Config.Port, ex.Message);

                // Brief delay to prevent CPU spinning on repeated errors
                _ = System.Threading.Tasks.Task.Delay(100, _cancellationToken);
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void HandleAccept(System.Net.Sockets.SocketAsyncEventArgs e)
    {
        if (e.SocketError == System.Net.Sockets.SocketError.Success &&
            e.AcceptSocket is System.Net.Sockets.Socket socket)
        {
            try
            {
                if (!socket.Connected || socket.Handle.ToInt64() == -1)
                {
                    this._logger.Warn("[TCP] Socket is invalid or disconnected");
                    this.SafeCloseSocket(socket);
                    return;
                }

                if (!_connectionLimiter.IsConnectionAllowed(
                    ((System.Net.IPEndPoint)socket.RemoteEndPoint!).Address))
                {
                    this.SafeCloseSocket(socket);
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
                _logger.Warn("[TCP] Socket was disposed during accept");

                this.SafeCloseSocket(socket);
                if (e is PooledSocketAsyncEventArgs pooled && pooled.Context != null)
                {
                    ObjectPoolManager.Instance.Return<PooledAcceptContext>(pooled.Context);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error("[TCP] Process accept error: {0}", ex.Message);
                try { socket.Close(); } catch { }
                ObjectPoolManager.Instance.Return<PooledAcceptContext>(((PooledSocketAsyncEventArgs)e).Context!);
            }
        }
        else
        {
            _logger.Warn("[TCP] Accept failed: {0}", e.SocketError);
            if (e is PooledSocketAsyncEventArgs pooled)
            {
                ObjectPoolManager.Instance.Return<PooledAcceptContext>(pooled.Context!); // 💥 TH AcceptSocket == null
            }
        }
    }

    private void SafeCloseSocket(System.Net.Sockets.Socket socket)
    {
        try
        {
            socket?.Close();
        }
        catch (System.Exception ex)
        {
            _logger.Debug("[TCP] Error closing socket: {0}", ex.Message);
        }
    }
}