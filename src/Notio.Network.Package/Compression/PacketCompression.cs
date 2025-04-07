using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Common.Security;
using Notio.Extensions.Primitives;
using Notio.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Compression;

/// <summary>
/// Provides helper methods for compressing and decompressing packet payloads.
/// </summary>
public static class PacketCompression
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CompressPayload(in Packet packet, CompressionType compressionType = CompressionType.GZip)
    {
        ValidatePacketForCompression(packet);

        try
        {
            Memory<byte> compressedData = compressionType switch
            {
                CompressionType.GZip => CompressGZip(packet.Payload),
                CompressionType.Brotli => BrotliCompressor.Compress(packet.Payload),
                CompressionType.Deflate => CompressDeflate(packet.Payload),
                _ => throw new PackageException($"Unsupported compression type: {compressionType}"),
            };
            return CreateCompressedPacket(packet, compressedData);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet DecompressPayload(in Packet packet, CompressionType compressionType = CompressionType.GZip)
    {
        ValidatePacketForDecompression(packet);

        try
        {
            Memory<byte> decompressedData = compressionType switch
            {
                CompressionType.GZip => DecompressGZip(packet.Payload),
                CompressionType.Brotli => BrotliCompressor.Decompress(packet.Payload),
                CompressionType.Deflate => DecompressDeflate(packet.Payload),
                _ => throw new PackageException($"Unsupported compression type: {compressionType}"),
            };
            return CreateDecompressedPacket(packet, decompressedData);
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

    private static Packet CreateCompressedPacket(Packet packet, Memory<byte> compressedData) =>
        new(packet.Id, packet.Checksum, packet.Timestamp, packet.Code,
            packet.Type, packet.Flags.AddFlag(PacketFlags.Compressed), packet.Priority, packet.Number, compressedData);

    private static Packet CreateDecompressedPacket(Packet packet, Memory<byte> decompressedData) =>
        new(packet.Id, packet.Checksum, packet.Timestamp, packet.Code,
            packet.Type, packet.Flags.RemoveFlag(PacketFlags.Compressed), packet.Priority, packet.Number, decompressedData);

    // Helper methods for GZip compression and decompression using pointers
    private static unsafe Memory<byte> CompressGZip(ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using var memoryStream = new MemoryStream();
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress);
            var span = new ReadOnlySpan<byte>(dataPtr, data.Length);
            gzipStream.Write(span);
            gzipStream.Close();
            return new Memory<byte>(memoryStream.ToArray());
        }
    }

    private static unsafe Memory<byte> DecompressGZip(ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using MemoryStream memoryStream = new(data.ToArray());
            using GZipStream gzipStream = new(memoryStream, CompressionMode.Decompress);
            using MemoryStream outputStream = new();
            var span = new ReadOnlySpan<byte>(dataPtr, data.Length);
            gzipStream.CopyTo(outputStream);
            return new Memory<byte>(outputStream.ToArray());
        }
    }

    // Helper methods for Deflate compression and decompression using pointers
    private static unsafe Memory<byte> CompressDeflate(ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using MemoryStream memoryStream = new();
            using DeflateStream deflateStream = new(memoryStream, CompressionMode.Compress);
            var span = new ReadOnlySpan<byte>(dataPtr, data.Length);
            deflateStream.Write(span);
            deflateStream.Close();
            return new Memory<byte>(memoryStream.ToArray());
        }
    }

    private static unsafe Memory<byte> DecompressDeflate(ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using MemoryStream memoryStream = new(data.ToArray());
            using DeflateStream deflateStream = new(memoryStream, CompressionMode.Decompress);
            using MemoryStream outputStream = new();
            var span = new ReadOnlySpan<byte>(dataPtr, data.Length);
            deflateStream.CopyTo(outputStream);
            return new Memory<byte>(outputStream.ToArray());
        }
    }

}
