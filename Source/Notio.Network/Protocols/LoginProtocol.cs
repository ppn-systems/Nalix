using Notio.Common.Connection;
using Notio.Network.Handlers;

namespace Notio.Network.Protocols;

public class LoginProtocol(PacketHandlerRouter packetHandlerRouter) : Protocol
{
    private readonly PacketHandlerRouter _packetHandlerRouter = packetHandlerRouter;

    /// <inheritdoc />
    public override bool KeepConnectionOpen => false;

    /// <inheritdoc />
    public override void ProcessMessage(object sender, IConnectEventArgs args)
    {
        _packetHandlerRouter.RoutePacket(args.Connection);
    }

    /// <inheritdoc />
    public override string ToString() => "Login Protocol";
}