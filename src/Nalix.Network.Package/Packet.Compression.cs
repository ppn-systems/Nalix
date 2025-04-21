using Nalix.Common.Compression;
using Nalix.Common.Package;
using Nalix.Network.Package.Engine;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketCompressor<Packet>
{
    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Compress(Packet packet, CompressionType type)
        => PacketCompact.Compress(packet, type);

    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Decompress(Packet packet, CompressionType type)
        => PacketCompact.Decompress(packet, type);
}
