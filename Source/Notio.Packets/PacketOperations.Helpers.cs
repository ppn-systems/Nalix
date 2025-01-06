using System.Runtime.CompilerServices;

namespace Notio.Packets;

public static partial class PacketOperations
{
    /// <summary>
    /// Tạo một bản sao độc lập của Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Clone(this in Packet packet)
        => new(packet.Type, packet.Flags, packet.Command, packet.Payload.ToArray());
}
