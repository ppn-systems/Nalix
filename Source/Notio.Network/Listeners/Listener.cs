using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

public abstract class Listener : TcpListener, IListener
{
    private static readonly NetworkConfig NetworkConfig = ConfiguredShared.Instance.Get<NetworkConfig>();

    private readonly int _port;
    private readonly ILogger? _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;

    /// <inheritdoc />
    public Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger? logger)
        : base(IPAddress.Any, port)
    {
        _port = port;
        _protocol = protocol;
        _logger = logger;
        _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
    }

    /// <inheritdoc />
    public Listener(IProtocol protocol, IBufferPool bufferPool, ILogger? logger)
        : base(IPAddress.Any, NetworkConfig.Port)
    {
        _logger = logger;
        _protocol = protocol;
        _port = NetworkConfig.Port;
        _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
    }

    /// <inheritdoc />
    public void BeginListening(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            try
            {
                base.Start();
            }
            catch (SocketException ex)
            {
                _logger?.Error($"Could not start {_protocol} on port {_port}", ex);
                Environment.Exit(1);
                return;
            }

            _logger?.Info($"{_protocol} is online on port {_port}");

            while (!cancellationToken.IsCancellationRequested)
            {
                IConnection connection = await CreateConnection(cancellationToken);

                _protocol.OnAccept(connection);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public void EndListening() => base.Stop();

    private async Task<IConnection> CreateConnection(CancellationToken cancellationToken)
    {
        Socket socket = await AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
        SocketConfig(socket);

        Connection.Connection connection = new(socket, _bufferPool, _logger); // Fully qualify the Connection class

        connection.OnCloseEvent += this.OnConnectionClose!;
        connection.OnProcessEvent += _protocol.ProcessMessage!;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage!;
        return connection;
    }

    private void OnConnectionClose(object? sender, IConnectEventArgs args)
    {
        // De-subscribe to this event first.
        args.Connection.OnCloseEvent -= this.OnConnectionClose!;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage!;
    }

    private static void SocketConfig(Socket socket)
    {
        socket.ReceiveBufferSize = NetworkConfig.ReceiveBufferSize;
        socket.SendBufferSize = NetworkConfig.SendBufferSize;
        socket.LingerState = new LingerOption(true, NetworkConfig.LingerTimeoutSeconds);
        socket.ReceiveTimeout = NetworkConfig.ReceiveTimeoutMilliseconds;
        socket.SendTimeout = NetworkConfig.SendTimeoutMilliseconds;

        // Apply TCP-specific settings
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, NetworkConfig.KeepAlive);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, NetworkConfig.NoDelay);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, NetworkConfig.ReuseAddress);

        if (NetworkConfig.DualMode)
        {
            socket.DualMode = true;  // Enable IPv6 and IPv4 support
        }

        // Configure blocking mode
        socket.Blocking = NetworkConfig.IsBlocking;

        // Handle low-watermark thresholds (if needed in the implementation)
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, NetworkConfig.SocketReceiveLowWatermark);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, NetworkConfig.SocketSendLowWatermark);
    }
}