using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

public abstract class Listener : TcpListener, IListener
{
    private static readonly NetworkConfig _networkConfig = ConfiguredShared.Instance.Get<NetworkConfig>();

    private readonly int _port;
    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _bufferPool;

    /// <inheritdoc />
    public Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : base(IPAddress.Any, port)
    {
        _port = port;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
    }

    /// <inheritdoc />
    public Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : base(IPAddress.Any, _networkConfig.Port)
    {
        _port = _networkConfig.Port;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
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
                _logger?.Info($"{_protocol} is online on port {_port}");
                while (!cancellationToken.IsCancellationRequested)
                {
                    IConnection connection = await CreateConnection(cancellationToken);

                    _protocol.OnAccept(connection);
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.Info($"Listener on port {_port} stopped gracefully");
            }
            catch (SocketException ex)
            {
                _logger?.Error($"Could not start {_protocol} on port {_port}", ex);
                Environment.Exit(1);
                return;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Critical error in listener on port {_port}", ex);
                Environment.Exit(1);
                return;
            }
            finally
            {
                base.Stop();
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public void EndListening() => base.Stop();

    private async Task<IConnection> CreateConnection(CancellationToken cancellationToken)
    {
        Socket socket = await AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
        ConfigureHighPerformanceSocket(socket);

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

    private static void ConfigureHighPerformanceSocket(Socket socket)
    {
        socket.LingerState = new LingerOption(false, 0); // Không delay khi đóng
        socket.NoDelay = true;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Cấu hình cho hiệu năng cao
        socket.ReceiveBufferSize = ushort.MaxValue;
        socket.SendBufferSize = ushort.MaxValue;
        socket.ReceiveTimeout = 30000;
        socket.SendTimeout = 30000;

        // Tối ưu cho các kết nối ngắn hạn
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            socket.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveValues(), null);
        }
    }

    private static byte[] GetKeepAliveValues()
    {
        const uint time = 30000; // 30s
        const uint interval = 1000; // 1s
        return
        [
        1, 0, 0, 0,
        (byte)(time & 0xFF), (byte)((time >> 8) & 0xFF),
        (byte)((time >> 16) & 0xFF), (byte)((time >> 24) & 0xFF),
        (byte)(interval & 0xFF), (byte)((interval >> 8) & 0xFF),
        (byte)((interval >> 16) & 0xFF), (byte)((interval >> 24) & 0xFF)
        ];
    }
}