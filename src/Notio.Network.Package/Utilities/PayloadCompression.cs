using Notio.Network.Package.Enums;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities;

/// <summary>
/// Provides methods to compress and decompress raw payload data using multiple compression algorithms.
/// </summary>
public static class PayloadCompression
{
    private const int BufferSize = 8192; // 8KB buffer for streaming

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
        if (data.IsEmpty) return [];

        using MemoryStream outputStream = new(data.Length / 2); // Preallocate capacity
        try
        {
            Stream compressionStream = algorithm switch
            {
                PacketCompressionMode.GZip => new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true),
                PacketCompressionMode.Deflate => new DeflateStream(outputStream, CompressionLevel.Optimal, leaveOpen: true),
                PacketCompressionMode.Brotli => new BrotliStream(outputStream, CompressionLevel.Optimal, leaveOpen: true),
                _ => throw new InvalidOperationException("Unsupported compression algorithm.")
            };

            using (compressionStream)
            {
                compressionStream.Write(data.Span);
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
        if (compressedData.IsEmpty) return [];

        using MemoryStream inputStream = new(compressedData.Length);
        inputStream.Write(compressedData.Span);
        inputStream.Position = 0;

        using MemoryStream outputStream = new(compressedData.Length * 2); // Preallocate capacity
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            Stream decompressionStream = algorithm switch
            {
                PacketCompressionMode.GZip => new GZipStream(inputStream, CompressionMode.Decompress),
                PacketCompressionMode.Deflate => new DeflateStream(inputStream, CompressionMode.Decompress),
                PacketCompressionMode.Brotli => new BrotliStream(inputStream, CompressionMode.Decompress),
                _ => throw new InvalidOperationException("Unsupported decompression algorithm.")
            };

            using (decompressionStream)
            {
                int bytesRead;
                while ((bytesRead = decompressionStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outputStream.Write(buffer, 0, bytesRead);
                }
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
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
