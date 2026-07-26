using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Hosting.Tests.HandlerScan;

[Packet]
[GenerateFormatter]
[SerializePackable(SerializeLayout.Explicit)]
public sealed partial class HandlerScanPacket : PacketBase<HandlerScanPacket>, IPacketStaticOpcode
{
    public static ushort StaticOpCode => 8888;

    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}
