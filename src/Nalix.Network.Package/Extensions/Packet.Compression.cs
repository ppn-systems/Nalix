using Nalix.Network.Package.Engine;

namespace Nalix.Network.Package.Extensions;

/// <summary>
/// Provides operations for compressing and decompressing packets.
/// </summary>
public static class PacketCompression
{
    /// <summary>
    /// Compresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet? CompressPayload(this in Packet packet)
        => PacketCompact.Compress(packet);

    /// <summary>
    /// Decompresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet? DecompressPayload(this in Packet packet)
        => PacketCompact.Decompress(packet);
}
