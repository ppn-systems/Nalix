using Nalix.Abstractions.Serialization;

using System;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Serialization;
using Nalix.Codec.DataFrames;

namespace Nalix.Codec.Tests.DataFrames
{
    /// <summary>
    /// Test-only packet for namespace scanning.
    /// </summary>
    public sealed class AssemblyScanRootPacket : PacketBase<AssemblyScanRootPacket>
    {
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
    public sealed class AssemblyScanChildPacket : PacketBase<AssemblyScanChildPacket>
    {
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        public static new AssemblyScanChildPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<AssemblyScanChildPacket>.Deserialize(buffer);
    }
}

















