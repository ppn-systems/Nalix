using Nalix.Common.Packets;
using Nalix.Network.Package.Engine;

namespace Nalix.Network.Package;

public readonly partial struct Packet : IPacketCompressor<Packet>
{
    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketCompressor<Packet>.Compress(Packet packet)
        => PacketCompact.Compress(packet);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    static Packet IPacketCompressor<Packet>.Decompress(Packet packet)
        => PacketCompact.Decompress(packet);
}
