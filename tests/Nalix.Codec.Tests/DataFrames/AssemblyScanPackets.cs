using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Codec.Tests.DataFrames
{
    /// <summary>
    /// Test-only packet for namespace scanning.
    /// </summary>
    [GenerateFormatter]
    public sealed partial class AssemblyScanRootPacket : PacketBase<AssemblyScanRootPacket>, IPacketStaticOpcode
    {
        public static ushort StaticOpCode => 9999;
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        public static new AssemblyScanRootPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<AssemblyScanRootPacket>.Deserialize(buffer);
    }
}

namespace Nalix.Codec.Tests.DataFrames.AssemblyScanChild
{
    /// <summary>
    /// Test-only child namespace packet for recursive scanning.
    /// </summary>
    [GenerateFormatter]
    public sealed partial class AssemblyScanChildPacket : PacketBase<AssemblyScanChildPacket>, IPacketStaticOpcode
    {
        public static ushort StaticOpCode => 9999;
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        public static new AssemblyScanChildPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<AssemblyScanChildPacket>.Deserialize(buffer);
    }
}





















