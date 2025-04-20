using Notio.Common.Package;
using Notio.Common.Security;

namespace Notio.Network.Package;

public readonly partial struct Packet
{
    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Compress(Packet packet, CompressionMode type)
        => Utilities.PacketCompression.CompressPayload(packet, type);

    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Decompress(Packet packet, CompressionMode type)
        => Utilities.PacketCompression.DecompressPayload(packet, type);
}
