using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Listener"/> class with the specified port, protocol, buffer pool, and logger.
/// </remarks>
/// <param name="port">The port to listen on.</param>
/// <param name="protocol">The protocol to handle the connections.</param>
/// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
/// <param name="logger">The logger to log events and errors.</param>
public abstract class Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    : IListener, IDisposable
{
    private const int True = 1;
    private const int False = 0;

    // Using lazy initialization for thread-safety and singleton pattern
    private static readonly Lazy<ListenerConfig> _lazyConfig = new(() =>
        ConfiguredShared.Instance.Get<ListenerConfig>());

    private static ListenerConfig Config => _lazyConfig.Value;

    private readonly int _port = port;
    private readonly SemaphoreSlim _listenerLock = new(1, 1);
    private readonly TcpListener _tcpListener = new(IPAddress.Any, port);
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IProtocol _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
    private readonly IBufferPool _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));

    private bool _isDisposed;
    private Task? _listenerTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    public bool IsListening => _listenerTask != null &&
        !(_listenerTask.IsCanceled || _listenerTask.IsCompleted || _listenerTask.IsFaulted);

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : this(Config.Port, protocol, bufferPool, logger)
    {
    }

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public void BeginListening(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed || _tcpListener.Server == null, this);

        if (IsListening)
            return;

        _logger.Debug("Starting to listen for incoming connections.");

        // Create a linked token source to combine external cancellation with internal cancellation
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        _listenerTask = Task.Run(async () =>
        {
            await _listenerLock.WaitAsync(linkedToken).ConfigureAwait(false);
            try
            {
                _tcpListener.Start();
                _logger.Info($"{_protocol} is online on port {_port}");

                // Create multiple accept tasks in parallel for higher throughput
                const int maxParallelAccepts = 5;
                var acceptTasks = new Task[maxParallelAccepts];

                for (int i = 0; i < maxParallelAccepts; i++)
                {
                    acceptTasks[i] = AcceptConnectionsAsync(linkedToken);
                }

                await Task.WhenAll(acceptTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Listener on port {_port} stopped gracefully");
            }
            catch (SocketException ex)
            {
                _logger.Error($"Could not start {_protocol} on port {_port}", ex);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Critical error in listener on port {_port}", ex);
                // Don't call Environment.Exit here, instead let the exception propagate
                // so the host application can decide how to handle it
                throw;
            }
            finally
            {
                _tcpListener.Stop();
                _listenerLock.Release();
                _logger.Debug("Stopped listening for incoming connections.");
            }
        }, linkedToken);
    }

    /// <summary>
    /// Accepts connections in a loop until cancellation is requested
    /// </summary>
    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IConnection connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
                // Await the ValueTask to ensure it is used properly
                await ProcessConnectionAsync(connection).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Error($"Error accepting connection on port {_port}", ex);
                // Brief delay to prevent CPU spinning on repeated errors
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.Debug("Stopped accepting incoming connections.");
    }

    /// <summary>
    /// Processes a new connection using the protocol handler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask ProcessConnectionAsync(IConnection connection)
    {
        try
        {
            _logger.Debug($"Processing new connection from {connection.RemoteEndPoint}");
            _protocol.OnAccept(connection);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing new connection from {connection.RemoteEndPoint}", ex);
            connection.Close();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void EndListening()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _logger.Debug("Stopping the listener.");

        _cts?.Cancel();

        if (_tcpListener?.Server != null)
        {
            _logger.Info($"Stopping listener on port {_port}");
            _tcpListener.Stop();
        }

        // Wait for the listener task to complete with a timeout
        _listenerTask?.Wait(TimeSpan.FromSeconds(5));
        _logger.Debug("Listener stopped.");
    }

    /// <summary>
    /// Creates a new connection from an incoming socket.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the connection creation process.</param>
    /// <returns>A task representing the connection creation.</returns>
    private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        Socket socket = await _tcpListener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
        ConfigureHighPerformanceSocket(socket);

        var connection = new Connection.Connection(socket, _bufferPool, _logger);

        // Use weak event pattern to avoid memory leaks
        connection.OnCloseEvent += OnConnectionClose;
        connection.OnProcessEvent += _protocol.ProcessMessage!;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage!;
        return connection;
    }

    /// <summary>
    /// Handles the closure of a connection by unsubscribing from its events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    private void OnConnectionClose(object? sender, IConnectEventArgs args)
    {
        _logger.Debug($"Closing connection from {args.Connection.RemoteEndPoint}");
        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= OnConnectionClose;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage!;

        args.Connection.Dispose();
    }

    /// <summary>
    /// Configures the socket for high-performance operation by setting buffer sizes, timeouts, and keep-alive options.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConfigureHighPerformanceSocket(Socket socket)
    {
        // Performance tuning
        socket.NoDelay = Config.NoDelay;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            socket.SetSocketOption(SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress, Config.ReuseAddress ? True : False);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            if (_tcpListener?.Server != null)
            {
                _logger.Info($"Stopping listener on port {_port}");
                _tcpListener.Stop();
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _listenerLock.Dispose();
        }

        _isDisposed = true;
        _logger.Debug("Disposed the listener.");
    }
}
