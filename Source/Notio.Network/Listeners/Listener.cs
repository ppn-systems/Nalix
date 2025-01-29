using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

public abstract class Listener(NetworkConfig networkCfg, IProtocol protocol)
    : TcpListener(IPAddress.Any, networkCfg.Port), IListener
{
    private readonly int _port = networkCfg.Port;
    private readonly IProtocol _protocol = protocol;
    private readonly ILogger? _logger = networkCfg.Logger;

    private readonly IBufferPool _bufferPool = networkCfg.BufferPool
        ?? throw new ArgumentNullException(nameof(networkCfg), "Buffer pool cannot be null.");

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
}