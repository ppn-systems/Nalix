using Notio.Common.Connection;
using Notio.Common.Connection.Args;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

public abstract class Listener(int port, IProtocol protocol, IBufferPool bufferAllocator, ILogger? logger)
    : TcpListener(IPAddress.Any, port), IListener
{
    private readonly int _port = port;
    private readonly ILogger? _logger = logger;
    private readonly IProtocol _protocol = protocol;
    private readonly IBufferPool _bufferAllocator = bufferAllocator;

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

    public void EndListening() => base.Stop();

    private async Task<IConnection> CreateConnection(CancellationToken cancellationToken)
    {
        Socket socket = await AcceptSocketAsync(cancellationToken).ConfigureAwait(false);

        Connection.Connection connection = new(socket, _bufferAllocator, _logger); // Fully qualify the Connection class

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
}