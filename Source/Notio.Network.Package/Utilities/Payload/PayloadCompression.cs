using Notio.Network.Package.Enums;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities.Payload;

/// <summary>
/// Provides methods to compress and decompress raw payload data using multiple compression algorithms.
/// </summary>
public static class PayloadCompression
{
    /// <summary>
    /// Compresses the given data using the specified algorithm.
    /// </summary>
    /// <param name="data">The data to compress.</param>
    /// <param name="algorithm">The compression algorithm to use.</param>
    /// <returns>Compressed data as a byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown if compression fails.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Compress(ReadOnlyMemory<byte> data, PacketCompressionMode algorithm)
    {
        try
        {
            using MemoryStream outputStream = new();
            switch (algorithm)
            {
                case PacketCompressionMode.GZip:
                    using (GZipStream gzipStream = new(outputStream, CompressionLevel.Optimal, leaveOpen: true))
                        gzipStream.Write(data.Span);
                    break;

                case PacketCompressionMode.Deflate:
                    using (DeflateStream deflateStream = new(outputStream, CompressionLevel.Optimal, leaveOpen: true))
                        deflateStream.Write(data.Span);
                    break;

                case PacketCompressionMode.Brotli:
                    using (BrotliStream brotliStream = new(outputStream, CompressionLevel.Optimal, leaveOpen: true))
                        brotliStream.Write(data.Span);
                    break;

                default:
                    throw new InvalidOperationException("Unsupported compression algorithm.");
            }

            return outputStream.ToArray();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new InvalidOperationException("Error occurred during data compression.", ex);
        }
    }

    /// <summary>
    /// Decompresses the given compressed data using the specified algorithm.
    /// </summary>
    /// <param name="compressedData">The compressed data.</param>
    /// <param name="algorithm">The compression algorithm used.</param>
    /// <returns>Decompressed data as a byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown if decompression fails.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Decompress(ReadOnlyMemory<byte> compressedData, PacketCompressionMode algorithm)
    {
        try
        {
            using MemoryStream inputStream = new(compressedData.ToArray());
            using MemoryStream outputStream = new();

            switch (algorithm)
            {
                case PacketCompressionMode.GZip:
                    using (GZipStream gzipStream = new(inputStream, CompressionMode.Decompress))
                        gzipStream.CopyTo(outputStream);
                    break;

                case PacketCompressionMode.Deflate:
                    using (DeflateStream deflateStream = new(inputStream, CompressionMode.Decompress))
                        deflateStream.CopyTo(outputStream);
                    break;

                case PacketCompressionMode.Brotli:
                    using (BrotliStream brotliStream = new(inputStream, CompressionMode.Decompress))
                        brotliStream.CopyTo(outputStream);
                    break;

                default:
                    throw new InvalidOperationException("Unsupported decompression algorithm.");
            }

            return outputStream.ToArray();
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException("Invalid compressed data.", ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new InvalidOperationException("Error occurred during data decompression.", ex);
        }
    }
}