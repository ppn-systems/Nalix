using Nalix.Abstractions.Serialization;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Network.Tests.HostingScan;

public sealed class HostingScanPacket : PacketBase<HostingScanPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}

[Packet]
public sealed class HostingScanAttributedPacket : PacketBase<HostingScanAttributedPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}














