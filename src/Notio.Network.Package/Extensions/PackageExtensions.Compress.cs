using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Network.Package.Helpers;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides operations for compressing and decompressing packets.
/// </summary>
public static partial class PackageExtensions
{
    /// <summary>
    /// Compresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Compress(this in Packet packet)
        => PacketCompressionHelper.CompressPayload(packet);

    /// <summary>
    /// Decompresses the payload of the packet using the specified compression algorithm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Decompress(this in Packet packet)
        => PacketCompressionHelper.DecompressPayload(packet);

    /// <summary>
    /// Tries to compress the payload of the packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompress(this Packet @this, out IPacket? @out)
    {
        try
        {
            @out = @this.Compress();
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
    public static bool TryDecompress(this Packet @this, out IPacket? @out)
    {
        try
        {
            @out = @this.Decompress();
            return true;
        }
        catch (PackageException)
        {
            @out = null;
            return false;
        }
    }
}
