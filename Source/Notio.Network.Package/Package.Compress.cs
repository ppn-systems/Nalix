using Notio.Common.Exceptions;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Helpers;
using Notio.Network.Package.Utilities;
using Notio.Network.Package.Utilities.Payload;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package;

/// <summary>
/// Provides operations for compressing and decompressing packets.
/// </summary>
public static partial class Package
{
    /// <summary>
    /// Compresses the payload of the packet using the specified compression algorithm.
    /// (Nén payload của packet bằng thuật toán chỉ định.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(this in Packet @this, PacketCompressionMode mode = PacketCompressionMode.GZip)
    {
        PacketVerifier.ValidateCompressionEligibility(@this);

        try
        {
            byte[] compressedData = PayloadCompression.Compress(@this.Payload, mode);

            return new Packet(
                @this.Type,
                @this.Flags.AddFlag(PacketFlags.IsCompressed),
                @this.Priority,
                @this.Command,
                compressedData
            );
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
    public static Packet DecompressPayload(this in Packet @this, PacketCompressionMode mode = PacketCompressionMode.GZip)
    {
        PacketVerifier.ValidateCompressionEligibility(@this);

        if (!@this.Flags.HasFlag(PacketFlags.IsCompressed))
            throw new PackageException("Payload is not marked as compressed.");

        try
        {
            byte[] decompressedData = PayloadCompression.Decompress(@this.Payload, mode);

            return new Packet(
                @this.Type,
                @this.Flags.RemoveFlag(PacketFlags.IsCompressed),
                @this.Priority,
                @this.Command,
                decompressedData
            );
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
    public static bool TryCompressPayload(this Packet @this, out Packet @out, PacketCompressionMode mode = PacketCompressionMode.GZip)
    {
        try
        {
            @out = @this.CompressPayload(mode);
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
    public static bool TryDecompressPayload(this Packet @this, out Packet @out, PacketCompressionMode mode = PacketCompressionMode.GZip)
    {
        try
        {
            @out = @this.DecompressPayload(mode);
            return true;
        }
        catch (PackageException)
        {
            @out = default;
            return false;
        }
    }
}