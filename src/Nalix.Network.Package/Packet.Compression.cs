using Nalix.Common.Package;
using Nalix.Network.Package.Engine;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketCompressor<Packet>
{
    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Compress(Packet packet)
        => PacketCompact.Compress(packet);

    /// <inheritdoc />
    static Packet IPacketCompressor<Packet>.Decompress(Packet packet)
        => PacketCompact.Decompress(packet);
}
