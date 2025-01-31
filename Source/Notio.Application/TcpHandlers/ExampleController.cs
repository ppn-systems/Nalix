using Notio.Network.Handlers;
using Notio.Network.Package;

namespace Notio.Application.TcpHandlers;

public class ExampleController : PacketController
{
    [PacketCommand(100)]
    public static Packet ExampleMethod(Packet packet)
    {
        return new Packet(packet.Type, packet.Flags, packet.Priority, packet.Command, new byte[] { 1, 23, 3, 2, 45, 65 });
    }
}