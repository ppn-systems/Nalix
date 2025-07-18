using Nalix.Common.Connection;
using Nalix.Network.Connection;
using Nalix.Network.Internal;
using Nalix.Shared.Memory.Pooling;
using System.Net;

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
            {
                _ = tcs.TrySetResult(e.AcceptSocket!);
            }
            else
            {
                _ = tcs.TrySetException(new System.Net.Sockets.SocketException((System.Int32)e.SocketError));
            }
        };

    #endregion Fields

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IConnection InitializeConnection(System.Net.Sockets.Socket socket)
    {
        ConfigureHighPerformanceSocket(socket);

        IConnection connection = new Connection.Connection(socket, this._bufferPool, this._logger);

        connection.EnforceLimiterOnClose(this._connectionLimiter);
        connection.OnCloseEvent += this.HandleConnectionClose;
        connection.OnProcessEvent += this._protocol.ProcessMessage!;
        connection.OnPostProcessEvent += this._protocol.PostProcessMessage!;

        return connection;
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
        this._logger.Debug("[TCP] Closing {0}", args.Connection.RemoteEndPoint);
        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= this.HandleConnectionClose;
        args.Connection.OnProcessEvent -= this._protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= this._protocol.PostProcessMessage!;

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
    /// Synchronous method for accepting connections
    /// </summary>
    /// <param name="cancellationToken">Identifier for cancellation</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AcceptConnectionsSync(System.Threading.CancellationToken cancellationToken)
    {
        this._cancellationToken = cancellationToken;

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

                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
                    ProcessConnectionCallback, (this, connection));
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                this._logger.Error("[TCP] Accept error on {0}: {1}", Config.Port, ex.Message);
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
            System.Net.Sockets.Socket socket;

            if (!this._listener.AcceptAsync(state.Args))
            {
                socket = state.Args.AcceptSocket!;

                if (!this._connectionLimiter.IsConnectionAllowed(
                   ((IPEndPoint)socket.RemoteEndPoint!).Address.ToString()))
                {
                    socket.Close();
                    throw new System.OperationCanceledException();
                }

                return state.Args.SocketError == System.Net.Sockets.SocketError.Success
                    ? this.InitializeConnection(state.Args.AcceptSocket!)
                    : throw new System.Net.Sockets.SocketException((System.Int32)state.Args.SocketError);
            }

            socket = await state.Tcs.Task.ConfigureAwait(false);

            if (!this._connectionLimiter.IsConnectionAllowed(
                   ((IPEndPoint)socket.RemoteEndPoint!).Address.ToString()))
            {
                socket.Close();
                throw new System.OperationCanceledException();
            }

            return this.InitializeConnection(socket);
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
        while (!this._cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Try accepting the connection asynchronously
                if (this._listener.AcceptAsync(args))
                {
                    break;
                }

                // If the connection has been received synchronously, process it immediately.
                this.HandleAccept(args);
            }
            catch (System.Net.Sockets.SocketException ex) when (
                ex.SocketErrorCode is System.Net.Sockets.SocketError.Interrupted or
                System.Net.Sockets.SocketError.ConnectionAborted)
            {
                // _udpListener was closed or interrupted
                break;
            }
            catch (System.ObjectDisposedException)
            {
                // _udpListener was disposed
                break;
            }
            catch (System.Exception ex) when (!this._cancellationToken.IsCancellationRequested)
            {
                this._logger.Error("[TCP] Accept error on {0}: {1}", Config.Port, ex.Message);
                // Brief delay to prevent CPU spinning on repeated errors
                _ = System.Threading.Tasks.Task.Delay(100, this._cancellationToken);
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
                if (!this._connectionLimiter.IsConnectionAllowed(
                   ((IPEndPoint)socket.RemoteEndPoint!).Address.ToString()))
                {
                    socket.Close();
                    return;
                }

                // Create and process connection similar to async version
                IConnection connection = this.InitializeConnection(socket);

                // Process the connection
                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
                    ProcessConnectionCallback, (this, connection));
            }
            catch (System.Exception ex)
            {
                this._logger.Error("[TCP] Process accept error: {0}", ex.Message);
                try { socket.Close(); } catch { }
            }
        }
        else
        {
            this._logger.Warn("[TCP] Accept failed: {0}", e.SocketError);
        }
    }
}