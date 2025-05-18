using Nalix.Common.Connection;

namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Handles the closure of a connection by unsubscribing from its events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClose(object? sender, IConnectEventArgs args)
    {
        _logger.Debug("Closing {0}", args.Connection.RemoteEndPoint);
        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= OnConnectionClose;
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
            _logger.Debug("New connection from {0}", connection.RemoteEndPoint);
            _protocol.OnAccept(connection);
        }
        catch (System.Exception ex)
        {
            _logger.Error("Process error from {0}: {1}", connection.RemoteEndPoint, ex.Message);
            connection.Close();
        }
    }

    /// <summary>
    /// Synchronous method for accepting connections
    /// </summary>
    /// <param name="cancellationToken">Token for cancellation</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void AcceptConnections(System.Threading.CancellationToken cancellationToken)
    {
        System.Net.Sockets.SocketAsyncEventArgs args = new();
        args.Completed += (sender, e) =>
        {
            HandleAccept(e);
            AcceptNext();
        };

        AcceptNext();

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void AcceptNext()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Reset SocketAsyncEventArgs
                    args.AcceptSocket = null;

                    // Try accepting the connection asynchronously
                    if (_listenerSocket.AcceptAsync(args)) break;

                    // If the connection has been received synchronously, process it immediately.
                    HandleAccept(args);
                }
                catch (System.Net.Sockets.SocketException ex) when (
                    ex.SocketErrorCode == System.Net.Sockets.SocketError.Interrupted ||
                    ex.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionAborted)
                {
                    // Socket was closed or interrupted
                    break;
                }
                catch (System.ObjectDisposedException)
                {
                    // Socket was disposed
                    break;
                }
                catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.Error("Accept error on {0}: {1}", _port, ex.Message);
                    // Brief delay to prevent CPU spinning on repeated errors
                    System.Threading.Tasks.Task.Delay(50, cancellationToken);
                }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void HandleAccept(System.Net.Sockets.SocketAsyncEventArgs e)
        {
            if (e.SocketError == System.Net.Sockets.SocketError.Success &&
                e.AcceptSocket is System.Net.Sockets.Socket socket)
            {
                try
                {
                    // Create and process connection similar to async version
                    IConnection connection = this.CreateConnection(socket);

                    // Process the connection
                    this.ProcessConnection(connection);
                }
                catch (System.Exception ex)
                {
                    _logger.Error("Process accept error: {0}", ex.Message);
                    try { socket.Close(); } catch { }
                }
            }
            else
            {
                _logger.Warn("Accept failed: {0}", e.SocketError);
            }
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

                this.ProcessConnection(connection);
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Error("Accept error on {0}: {1}", _port, ex.Message);
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
    /// <returns>A task representing the connection creation.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IConnection CreateConnection(System.Net.Sockets.Socket socket)
    {
        ConfigureHighPerformanceSocket(socket);

        IConnection connection = new Connection.Connection(socket, _buffer, _logger);

        // Use weak event pattern to avoid memory leaks
        connection.OnCloseEvent += OnConnectionClose;
        connection.OnProcessEvent += _protocol.ProcessMessage!;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage!;
        return connection;
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
    private async System.Threading.Tasks.Task<IConnection> CreateConnectionAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        System.Net.Sockets.Socket socket = await System.Threading.Tasks.Task.Factory
            .FromAsync(_listenerSocket.BeginAccept, _listenerSocket.EndAccept, null)
            .ConfigureAwait(false);

        await System.Threading.Tasks.Task.Yield();

        ConfigureHighPerformanceSocket(socket);

        Connection.Connection connection = new(socket, _buffer, _logger);

        // Use weak event pattern to avoid memory leaks
        connection.OnCloseEvent += OnConnectionClose;
        connection.OnProcessEvent += _protocol.ProcessMessage!;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage!;

        return connection;
    }
}
