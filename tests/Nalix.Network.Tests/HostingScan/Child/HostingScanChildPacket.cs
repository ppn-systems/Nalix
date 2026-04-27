using Nalix.Abstractions.Serialization;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Network.Tests.HostingScan.Child;

public sealed class HostingScanChildPacket : PacketBase<HostingScanChildPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}














