using Notio.Common.Exceptions;
using Notio.Common.Package;
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
    public static Packet CompressPayload(this in Packet packet)
        => Compression.PacketCompression.CompressPayload(packet);

    /// <summary>
    /// Decompresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet)
        => Compression.PacketCompression.DecompressPayload(packet);

    /// <summary>
    /// Tries to compress the payload of the packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompressPayload(this Packet @this, out IPacket? @out)
    {
        try
        {
            @out = @this.CompressPayload();
            return true;
        }
        catch (PackageException)
        {
            @out = null;
            return false;
        }
    }

    /// <summary>
    /// Tries to decompress the payload of the packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecompressPayload(this Packet @this, out IPacket? @out)
    {
        try
        {
            @out = @this.DecompressPayload();
            return true;
        }
        catch (PackageException)
        {
            @out = null;
            return false;
        }
    }
}
