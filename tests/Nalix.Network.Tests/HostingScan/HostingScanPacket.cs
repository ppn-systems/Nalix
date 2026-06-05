using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Network.Tests.HostingScan;

[GenerateFormatter]
public sealed partial class HostingScanPacket : PacketBase<HostingScanPacket>, IPacketStaticOpcode
{
    public static ushort StaticOpCode => 9999;
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}

[Packet]
[GenerateFormatter]
public sealed partial class HostingScanAttributedPacket : PacketBase<HostingScanAttributedPacket>, IPacketStaticOpcode
{
    public static ushort StaticOpCode => 9999;
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}
















