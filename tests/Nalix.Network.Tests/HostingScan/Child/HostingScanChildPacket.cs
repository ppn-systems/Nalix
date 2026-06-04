using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Network.Tests.HostingScan.Child;

[GenerateFormatter]
public sealed partial class HostingScanChildPacket : PacketBase<HostingScanChildPacket>, Nalix.Abstractions.Networking.Packets.IPacketStaticOpcode
{
    public static ushort StaticOpCode => 9999;
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}
















