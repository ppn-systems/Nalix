using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Network.Tests.HostingScan;

[GenerateFormatter]
public sealed partial class HostingScanPacket : PacketBase<HostingScanPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}

[Packet]
[GenerateFormatter]
public sealed partial class HostingScanAttributedPacket : PacketBase<HostingScanAttributedPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}














