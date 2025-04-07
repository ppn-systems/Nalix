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
    public static Packet CompressPayload(this in Packet packet, Common.Security.CompressionMode type)
        => Compression.PacketCompression.CompressPayload(packet, type);

    /// <summary>
    /// Decompresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet, Common.Security.CompressionMode type)
        => Compression.PacketCompression.DecompressPayload(packet, type);
}
