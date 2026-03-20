using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

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
