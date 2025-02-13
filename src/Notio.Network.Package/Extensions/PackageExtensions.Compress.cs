using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Utilities;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Extensions;

/// <summary>
/// Provides operations for compressing and decompressing packets.
/// </summary>
public static partial class PackageExtensions
{
    /// <summary>
    /// Compresses the payload of the packet using the specified compression algorithm.
    /// (Nén payload của packet bằng thuật toán chỉ định.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet packet)
    {
        PacketVerifier.ValidateCompressionEligibility(packet);

        try
        {
            byte[] compressedData = PayloadCompression.Compress(packet.Payload);

            return new Packet(packet.Id, packet.Type, packet.Flags.AddFlag(PacketFlags.IsCompressed),
                packet.Priority, packet.Command, packet.Timestamp, packet.Checksum, compressedData);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload compression.", ex);
        }
    }

    /// <summary>
    /// Decompresses the payload of the packet using the specified compression algorithm.
    /// (Giải nén payload của packet bằng thuật toán chỉ định.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(this in Packet packet)
    {
        PacketVerifier.ValidateCompressionEligibility(packet);

        if (!packet.Flags.HasFlag(PacketFlags.IsCompressed))
            throw new PackageException("Payload is not marked as compressed.");

        try
        {
            byte[] decompressedData = PayloadCompression.Decompress(packet.Payload);

            return new Packet(packet.Id, packet.Type, packet.Flags.RemoveFlag(PacketFlags.IsCompressed),
                packet.Priority, packet.Command, packet.Timestamp, packet.Checksum, decompressedData);
        }
        catch (InvalidDataException ex)
        {
            throw new PackageException("Invalid compressed data.", ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload decompression.", ex);
        }
    }

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
            @out = default;
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
            @out = default;
            return false;
        }
    }
}
