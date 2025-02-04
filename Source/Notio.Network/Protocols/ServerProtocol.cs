using Notio.Common.Connection;
using Notio.Network.Handlers;

namespace Notio.Network.Protocols;

public class ServerProtocol(PacketHandlerRouter packetCommandRouter) : Protocol
{
    private readonly PacketHandlerRouter _handlerFactory = packetCommandRouter;

    /// <inheritdoc />
    public override bool KeepConnectionOpen => true;

    /// <inheritdoc />
    public override void OnAccept(IConnection connection)
    {
        base.OnAccept(connection);
    }

    /// <inheritdoc />
    public override async void ProcessMessage(object sender, IConnectEventArgs args)
    {
        await _handlerFactory.RoutePacketAsync(args.Connection);
    }

    /// <inheritdoc />
    public override string ToString() => "Server Protocol";
}