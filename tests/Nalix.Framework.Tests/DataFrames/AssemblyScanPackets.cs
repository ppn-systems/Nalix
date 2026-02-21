
using System;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

namespace Nalix.Framework.Tests.DataFrames
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

namespace Nalix.Framework.Tests.DataFrames.AssemblyScanChild
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
