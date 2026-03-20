using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

namespace Nalix.Network.Tests.HostingScan.Child;

public sealed class HostingScanChildPacket : PacketBase<HostingScanChildPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }
}
