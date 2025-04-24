using Nalix.Common.Compression;
using Nalix.Common.Exceptions;
using Nalix.Common.Package.Enums;
using Nalix.Extensions.Primitives;
using Nalix.Utilities;

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
        CompressionType compressionType = CompressionType.GZip)
    {
        ValidatePacketForCompression(packet);

        try
        {
            System.Memory<byte> compressedData = compressionType switch
            {
                CompressionType.GZip => CompressGZip(packet.Payload),
                CompressionType.Brotli => BrotliCompressor.Compress(packet.Payload),
                CompressionType.Deflate => CompressDeflate(packet.Payload),
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
        CompressionType compressionType = CompressionType.GZip)
    {
        ValidatePacketForDecompression(packet);

        try
        {
            System.Memory<byte> decompressedData = compressionType switch
            {
                CompressionType.GZip => DecompressGZip(packet.Payload),
                CompressionType.Brotli => BrotliCompressor.Decompress(packet.Payload),
                CompressionType.Deflate => DecompressDeflate(packet.Payload),
                _ => throw new PackageException($"Unsupported compression type: {compressionType}"),
            };
            return CreateDecompressedPacket(packet, decompressedData);
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

    // Helper methods for GZip compression and decompression using pointers
    private static unsafe System.Memory<byte> CompressGZip(System.ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using System.IO.MemoryStream memoryStream = new();
            using System.IO.Compression.GZipStream gzipStream = new(
                memoryStream, System.IO.Compression.CompressionMode.Compress);

            System.ReadOnlySpan<byte> span = new(dataPtr, data.Length);
            gzipStream.Write(span);
            gzipStream.Close();
            return new System.Memory<byte>(memoryStream.ToArray());
        }
    }

    private static unsafe System.Memory<byte> DecompressGZip(System.ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using System.IO.MemoryStream memoryStream = new(data.ToArray());
            using System.IO.Compression.GZipStream gzipStream = new(
                memoryStream, System.IO.Compression.CompressionMode.Decompress);
            using System.IO.MemoryStream outputStream = new();

            System.ReadOnlySpan<byte> span = new(dataPtr, data.Length);
            gzipStream.CopyTo(outputStream);
            return new System.Memory<byte>(outputStream.ToArray());
        }
    }

    // Helper methods for Deflate compression and decompression using pointers
    private static unsafe System.Memory<byte> CompressDeflate(System.ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using System.IO.MemoryStream memoryStream = new();
            using System.IO.Compression.DeflateStream deflateStream = new(
                memoryStream, System.IO.Compression.CompressionMode.Compress);

            System.ReadOnlySpan<byte> span = new(dataPtr, data.Length);
            deflateStream.Write(span);
            deflateStream.Close();
            return new System.Memory<byte>(memoryStream.ToArray());
        }
    }

    private static unsafe System.Memory<byte> DecompressDeflate(System.ReadOnlyMemory<byte> data)
    {
        fixed (byte* dataPtr = data.Span)
        {
            using System.IO.MemoryStream memoryStream = new(data.ToArray());
            using System.IO.Compression.DeflateStream deflateStream = new(
                memoryStream, System.IO.Compression.CompressionMode.Decompress);
            using System.IO.MemoryStream outputStream = new();

            System.ReadOnlySpan<byte> span = new(dataPtr, data.Length);
            deflateStream.CopyTo(outputStream);
            return new System.Memory<byte>(outputStream.ToArray());
        }
    }

    #endregion Private Methods
}
