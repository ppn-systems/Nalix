using Notio.Common.Package;
using Notio.Common.Security;
using Notio.Network.Package.Engine;

namespace Notio.Network.Package;

public readonly partial struct Packet : IPacketCompressor<Packet>
{
    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Compress(Packet packet, CompressionMode type)
        => PacketCompact.Compress(packet, type);

    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Decompress(Packet packet, CompressionMode type)
        => PacketCompact.Decompress(packet, type);
}
