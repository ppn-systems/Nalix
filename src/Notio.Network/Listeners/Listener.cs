using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Config;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
public abstract class Listener : TcpListener, IListener
{
    private static readonly NetworkConfig NetworkConfig = ConfiguredShared.Instance.Get<NetworkConfig>();

    private readonly int _port;
    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class with the specified port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The port to listen on.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : base(IPAddress.Any, port)
    {
        _port = port;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : base(IPAddress.Any, NetworkConfig.Port)
    {
        _port = NetworkConfig.Port;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
    }

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public void BeginListening(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            try
            {
                Start();
                _logger.Info($"{_protocol} is online on port {_port}");
                while (!cancellationToken.IsCancellationRequested)
                {
                    IConnection connection = await CreateConnection(cancellationToken);

                    _protocol.OnAccept(connection);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Listener on port {_port} stopped gracefully");
            }
            catch (SocketException ex)
            {
                _logger.Error($"Could not start {_protocol} on port {_port}", ex);
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                _logger.Error($"Critical error in listener on port {_port}", ex);
                Environment.Exit(1);
            }
            finally
            {
                Stop();
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void EndListening() => Stop();

    /// <summary>
    /// Creates a new connection from an incoming socket.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the connection creation process.</param>
    /// <returns>A task representing the connection creation.</returns>
    private async Task<IConnection> CreateConnection(CancellationToken cancellationToken)
    {
        Socket socket = await AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
        ConfigureHighPerformanceSocket(socket);

        Connection.Connection connection = new(socket, _bufferPool, _logger); // Fully qualify the Connection class

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
        // De-subscribe to this event first.
        args.Connection.OnCloseEvent -= OnConnectionClose;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage!;
    }

    /// <summary>
    /// Configures the socket for high-performance operation.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    private static void ConfigureHighPerformanceSocket(Socket socket)
    {
        socket.LingerState = new LingerOption(false, NetworkConfig.LingerTimeoutSeconds); // No delay when closing
        socket.NoDelay = NetworkConfig.NoDelay;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, NetworkConfig.ReuseAddress);

        // Performance tuning for short-lived connections
        socket.SendBufferSize = NetworkConfig.SendBufferSize;
        socket.SendTimeout = NetworkConfig.SendTimeoutMilliseconds;
        socket.ReceiveBufferSize = NetworkConfig.ReceiveBufferSize;
        socket.ReceiveTimeout = NetworkConfig.ReceiveTimeoutMilliseconds;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveValues(), null);
    }

    /// <summary>
    /// Gets the keep-alive values for Windows sockets.
    /// </summary>
    /// <returns>A byte array containing the keep-alive values.</returns>
    private static byte[] GetKeepAliveValues()
    {
        const uint time = 30000; // 30s
        const uint interval = 1000; // 1s
        return
        [
            1, 0, 0, 0,
            (byte)(time & 0xFF), (byte)((time >> 8) & 0xFF), 0, 0,
            (byte)(interval & 0xFF), (byte)((interval >> 8) & 0xFF), 0, 0
        ];
    }
}
