using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Buffers;
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
public abstract class Listener : IListener, IDisposable
{
    // Using lazy initialization for thread-safety and singleton pattern
    private static readonly Lazy<ListenerConfig> _lazyConfig = new(() =>
        ConfiguredShared.Instance.Get<ListenerConfig>());

    private static ListenerConfig Config => _lazyConfig.Value;

    private readonly int _port;
    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;
    private readonly SemaphoreSlim _listenerLock = new(1, 1);
    private readonly TcpListener _tcpListener;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;
    private Task? _listenerTask;

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    public bool IsListening => _listenerTask != null && !(_listenerTask.IsCanceled || _listenerTask.IsCompleted || _listenerTask.IsFaulted);

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class with the specified port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The port to listen on.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    {
        _port = port;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
        _tcpListener = new TcpListener(IPAddress.Any, port);
    }

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
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (IsListening)
            return;

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
                // Use ValueTask.Run instead of Task.Run to reduce allocations for frequent operations
                _ = ProcessConnectionAsync(connection);
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
    }

    /// <summary>
    /// Processes a new connection using the protocol handler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask ProcessConnectionAsync(IConnection connection)
    {
        try
        {
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

        _cts?.Cancel();
        _tcpListener.Stop();

        // Wait for the listener task to complete with a timeout
        _listenerTask?.Wait(TimeSpan.FromSeconds(5));
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
        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= OnConnectionClose;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage!;
    }

    /// <summary>
    /// Configures the socket for high-performance operation by setting buffer sizes, timeouts, and keep-alive options.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConfigureHighPerformanceSocket(Socket socket)
    {
        // Caches configuration values to local variables to improve performance
        var config = Config;

        socket.LingerState = new LingerOption(false, config.LingerTimeoutSeconds);
        socket.NoDelay = config.NoDelay;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, config.ReuseAddress);

        // Performance tuning
        socket.SendBufferSize = config.SendBufferSize;
        socket.SendTimeout = config.SendTimeoutMilliseconds;
        socket.ReceiveBufferSize = config.ReceiveBufferSize;
        socket.ReceiveTimeout = config.ReceiveTimeoutMilliseconds;

        // Enable DualMode for IPv4 and IPv6 support if available
        if (Socket.OSSupportsIPv6)
        {
            socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Use ArrayPool to avoid allocations for temporary buffers
            byte[] keepAliveValues = ArrayPool<byte>.Shared.Rent(12);
            try
            {
                PrepareKeepAliveValues(keepAliveValues);
                socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keepAliveValues);
            }
        }
    }

    /// <summary>
    /// Prepares the keep-alive values for Windows sockets.
    /// </summary>
    /// <param name="buffer">A pre-allocated buffer to hold the keep-alive values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PrepareKeepAliveValues(Span<byte> buffer)
    {
        const uint time = 30000; // 30s
        const uint interval = 1000; // 1s

        buffer[0] = 1; // enable keep-alive
        buffer[1] = buffer[2] = buffer[3] = 0;

        // Time value
        buffer[4] = (byte)(time & 0xFF);
        buffer[5] = (byte)((time >> 8) & 0xFF);
        buffer[6] = buffer[7] = 0;

        // Interval value
        buffer[8] = (byte)(interval & 0xFF);
        buffer[9] = (byte)((interval >> 8) & 0xFF);
        buffer[10] = buffer[11] = 0;
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
            EndListening();
            _listenerLock.Dispose();
            _cts?.Dispose();
        }

        _isDisposed = true;
    }
}
