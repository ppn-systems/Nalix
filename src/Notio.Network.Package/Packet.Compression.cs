using Notio.Common.Compression;
using Notio.Common.Package;
using Notio.Network.Package.Engine;

namespace Notio.Network.Package;

public readonly partial struct Packet : IPacketCompressor<Packet>
{
    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Compress(Packet packet, CompressionType type)
        => PacketCompact.Compress(packet, type);

    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Decompress(Packet packet, CompressionType type)
        => PacketCompact.Decompress(packet, type);
}
