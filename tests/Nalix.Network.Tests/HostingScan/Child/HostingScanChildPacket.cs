using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Network.Tests.HostingScan.Child;

public sealed partial class HostingScanChildPacket : PacketBase<HostingScanChildPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}















