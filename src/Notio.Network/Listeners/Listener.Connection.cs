using Notio.Common.Connection;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Handles the closure of a connection by unsubscribing from its events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessConnection(IConnection connection)
    {
        try
        {
            _logger.Debug("New connection from {0}", connection.RemoteEndPoint);
            _protocol.OnAccept(connection);
        }
        catch (Exception ex)
        {
            _logger.Error("Process error from {0}: {1}", connection.RemoteEndPoint, ex.Message);
            connection.Close();
        }
    }

    /// <summary>
    /// Synchronous method for accepting connections
    /// </summary>
    /// <param name="cancellationToken">Token for cancellation</param>
    private void AcceptConnections(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Accept client socket synchronously with polling to support cancellation
                if (!_listenerSocket.Pending())
                {
                    Thread.Sleep(50); // Small delay to prevent CPU spinning
                    continue;
                }

                // Create and process connection similar to async version
                IConnection connection = this.CreateConnection(cancellationToken);

                // Process the connection
                this.ProcessConnection(connection);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted ||
                                             ex.SocketErrorCode == SocketError.ConnectionAborted)
            {
                // Socket was closed or interrupted
                break;
            }
            catch (ObjectDisposedException)
            {
                // Socket was disposed
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Error("Accept error on {0}: {1}", _port, ex.Message);
                // Brief delay to prevent CPU spinning on repeated errors
                Task.Delay(50, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Accepts connections in a loop until cancellation is requested
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Error("Accept error on {0}: {1}", _port, ex.Message);
                // Brief delay to prevent CPU spinning on repeated errors
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates a new connection from an incoming socket.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the connection creation process.</param>
    /// <returns>A task representing the connection creation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IConnection CreateConnection(CancellationToken cancellationToken)
    {
        Socket socket = _listenerSocket.AcceptSocket();

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        Socket socket = await _listenerSocket.AcceptSocketAsync(cancellationToken)
            .ConfigureAwait(false);

        ConfigureHighPerformanceSocket(socket);

        Connection.Connection connection = new(socket, _buffer, _logger);

        // Use weak event pattern to avoid memory leaks
        connection.OnCloseEvent += OnConnectionClose;
        connection.OnProcessEvent += _protocol.ProcessMessage!;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage!;
        return connection;
    }
}
