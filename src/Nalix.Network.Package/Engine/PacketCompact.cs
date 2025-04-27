using Nalix.Common.Compression;
using Nalix.Common.Exceptions;
using Nalix.Common.Package.Enums;
using Nalix.Extensions.Primitives;
using Nalix.Shared.LZ4;
using Nalix.Shared.LZ4.Internal;
using System;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// Provides helper methods for compressing and decompressing packet payloads.
/// </summary>
public static class PacketCompact
{
    /// <summary>
    /// Compresses the payload of the given packet using the specified compression type.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be compressed.</param>
    /// <param name="compressionType">The compression type to use.</param>
    /// <returns>A new <see cref="Packet"/> instance with the compressed payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if the packet is not eligible for compression, or if an error occurs during compression.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Compress(in Packet packet,
        CompressionType compressionType = CompressionType.LZ4)
    {
        ValidatePacketForCompression(packet);

        try
        {
            System.Memory<byte> compressedData = compressionType switch
            {
                CompressionType.LZ4 => CompressLZ4(packet.Payload.Span),
                _ => throw new PackageException($"Unsupported compression type: {compressionType}"),
            };
            return CreateCompressedPacket(packet, compressedData);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload compression.", ex);
        }
    }

    /// <summary>
    /// Decompresses the payload of the given packet using the specified compression type.
    /// </summary>
    /// <param name="packet">The packet whose payload needs to be decompressed.</param>
    /// <param name="compressionType">The compression type used for the payload.</param>
    /// <returns>A new <see cref="Packet"/> instance with the decompressed payload.</returns>
    /// <exception cref="PackageException">
    /// Thrown if the packet is not marked as compressed, if the payload is empty or null,
    /// or if an error occurs during decompression.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Packet Decompress(in Packet packet,
        CompressionType compressionType = CompressionType.LZ4)
    {
        ValidatePacketForDecompression(packet);

        try
        {
            System.Memory<byte> decompressedData = compressionType switch
            {
                CompressionType.LZ4 => (packet.Payload),
                _ => throw new PackageException($"Unsupported compression type: {compressionType}"),
            };
            return CreateDecompressedPacket(packet, CompressLZ4(packet.Payload.Span));
        }
        catch (System.IO.InvalidDataException ex)
        {
            throw new PackageException("Invalid compressed data.", ex);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.ObjectDisposedException)
        {
            throw new PackageException("Error occurred during payload decompression.", ex);
        }
    }

    #region Private Methods

    private static Memory<byte> CompressLZ4(ReadOnlySpan<byte> input)
    {
        // Estimate worst case size: input.Length + header + worst-case expansion
        int maxCompressedSize = Header.Size + input.Length + (input.Length / 255) + 16;
        byte[] buffer = new byte[maxCompressedSize];

        int compressedLength = LZ4Codec.Encode(input, buffer);

        if (compressedLength < 0)
            throw new PackageException("Compression failed due to insufficient buffer size.");

        return buffer.AsMemory(0, compressedLength);
    }

    private static Memory<byte> DecompressLZ4(ReadOnlySpan<byte> input)
    {
        if (input.Length < Header.Size)
            throw new PackageException("Compressed payload too small to contain a valid header.");

        Header header = MemOps.ReadUnaligned<Header>(input);

        if (header.OriginalLength < 0 || header.CompressedLength != input.Length)
            throw new PackageException("Invalid compressed data header.");

        byte[] buffer = new byte[header.OriginalLength];

        int decompressedLength = LZ4Codec.Decode(input, buffer);

        if (decompressedLength < 0)
            throw new PackageException("Decompression failed due to invalid data.");

        return buffer.AsMemory(0, decompressedLength);
    }

    private static void ValidatePacketForCompression(Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot compress an empty payload.");

        if (packet.Flags.HasFlag(PacketFlags.Encrypted))
            throw new PackageException("Payload is encrypted and cannot be compressed.");
    }

    private static void ValidatePacketForDecompression(Packet packet)
    {
        if (packet.Payload.IsEmpty)
            throw new PackageException("Cannot decompress an empty payload.");

        if (!packet.Flags.HasFlag(PacketFlags.Compressed))
            throw new PackageException("Payload is not marked as compressed.");
    }

    private static Packet CreateCompressedPacket(Packet packet, System.Memory<byte> compressedData) =>
        new(packet.Id, packet.Checksum, packet.Timestamp, packet.Code,
            packet.Type, packet.Flags.AddFlag(PacketFlags.Compressed),
            packet.Priority, packet.Number, compressedData, true);

    private static Packet CreateDecompressedPacket(Packet packet, System.Memory<byte> decompressedData) =>
        new(packet.Id, packet.Checksum, packet.Timestamp, packet.Code,
            packet.Type, packet.Flags.RemoveFlag(PacketFlags.Compressed),
            packet.Priority, packet.Number, decompressedData, true);

    #endregion Private Methods
}
