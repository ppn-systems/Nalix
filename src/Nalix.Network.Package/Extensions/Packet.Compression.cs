using Nalix.Common.Compression;

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
    public static Packet CompressPayload(this in Packet packet, CompressionType type)
        => Engine.PacketCompact.Compress(packet, type);

    /// <summary>
    /// Decompresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet, CompressionType type)
        => Engine.PacketCompact.Decompress(packet, type);
}
