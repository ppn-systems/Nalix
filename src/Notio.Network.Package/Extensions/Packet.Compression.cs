using Notio.Common.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides operations for compressing and decompressing packets.
/// </summary>
public static class PacketCompression
{
    /// <summary>
    /// Compresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet packet, CompressionType type)
        => Engine.PacketCompact.Compress(packet, type);

    /// <summary>
    /// Decompresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet, CompressionType type)
        => Engine.PacketCompact.Decompress(packet, type);
}
