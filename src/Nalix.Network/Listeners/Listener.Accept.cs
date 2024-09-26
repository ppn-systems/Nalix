using Nalix.Common.Connection;
using Nalix.Network.Listeners.Internal;
using Nalix.Shared.Memory.Pools;
using System.Net.Sockets;

namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
    #region Fields

    private static readonly System.Threading.WaitCallback ProcessConnectionCallback = static state =>
    {
        if (state is (Listener listener, IConnection conn))
        {
            listener.ProcessConnection(conn);
        }
        else
        {
            throw new System.NotImplementedException();
        }
    };

    internal static readonly System.EventHandler<
        System.Net.Sockets.SocketAsyncEventArgs> AsyncAcceptCompleted = static (s, e) =>
        {
            var tcs = (System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket>)e.UserToken!;
            if (e.SocketError == System.Net.Sockets.SocketError.Success)
                tcs.TrySetResult(e.AcceptSocket!);
            else
                tcs.TrySetException(new System.Net.Sockets.SocketException((System.Int32)e.SocketError));
        };

    #endregion Fields

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IConnection InitializeConnection(System.Net.Sockets.Socket socket)
    {
        ConfigureHighPerformanceSocket(socket);

        IConnection connection = new Connection.Connection(socket, _bufferPool, _logger);

        connection.OnCloseEvent += HandleConnectionClose;
        connection.OnProcessEvent += _protocol.ProcessMessage!;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage!;

        return connection;
    }

    /// <summary>
    /// Handles the closure of a connection by unsubscribing from its events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void HandleConnectionClose(object? sender, IConnectEventArgs args)
    {
        _logger.Debug("[TCP] Closing {0}", args.Connection.RemoteEndPoint);
        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= HandleConnectionClose;
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
            _logger.Debug("[TCP] New connection from {0}", connection.RemoteEndPoint);
            _protocol.OnAccept(connection);
        }
        catch (System.Exception ex)
        {
            _logger.Error("[TCP] Process error from {0}: {1}", connection.RemoteEndPoint, ex.Message);
            connection.Close();
        }
    }

    /// <summary>
    /// Synchronous method for accepting connections
    /// </summary>
    /// <param name="cancellationToken">Token for cancellation</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AcceptConnectionsSync(System.Threading.CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        System.Net.Sockets.SocketAsyncEventArgs args = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();
        args.Completed += this.OnSyncAcceptCompleted;

        this.AcceptNext(args);
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

                System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
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
        PooledAcceptContext state = ObjectPoolManager.Instance.Get<PooledAcceptContext>();

        try
        {
            if (!_listener.AcceptAsync(state.Args))
            {
                if (state.Args.SocketError == SocketError.Success)
                    return InitializeConnection(state.Args.AcceptSocket!);

                throw new SocketException((int)state.Args.SocketError);
            }

            Socket socket = await state.Tcs.Task.ConfigureAwait(false);
            return InitializeConnection(socket);
        }
        finally
        {
            ObjectPoolManager.Instance.Return(state);
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

        PooledSocketAsyncEventArgs newArgs = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();
        newArgs.Completed += this.OnSyncAcceptCompleted;
        this.AcceptNext(newArgs);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AcceptNext(System.Net.Sockets.SocketAsyncEventArgs args)
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Reset SocketAsyncEventArgs
                args.AcceptSocket = null;

                // Try accepting the connection asynchronously
                if (_listener.AcceptAsync(args)) break;

                // If the connection has been received synchronously, process it immediately.
                this.HandleAccept(args);
            }
            catch (System.Net.Sockets.SocketException ex) when (
                ex.SocketErrorCode == System.Net.Sockets.SocketError.Interrupted ||
                ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionAborted)
            {
                // _udpListener was closed or interrupted
                break;
            }
            catch (System.ObjectDisposedException)
            {
                // _udpListener was disposed
                break;
            }
            catch (System.Exception ex) when (!_cancellationToken.IsCancellationRequested)
            {
                _logger.Error("[TCP] Accept error on {0}: {1}", Config.Port, ex.Message);
                // Brief delay to prevent CPU spinning on repeated errors
                System.Threading.Tasks.Task.Delay(100, _cancellationToken);
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
                // Create and process connection similar to async version
                IConnection connection = this.InitializeConnection(socket);

                // Process the connection
                System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
                    ProcessConnectionCallback, (this, connection));
            }
            catch (System.Exception ex)
            {
                _logger.Error("[TCP] Process accept error: {0}", ex.Message);
                try { socket.Close(); } catch { }
            }
        }
        else
        {
            _logger.Warn("[TCP] Accept failed: {0}", e.SocketError);
        }
    }
}