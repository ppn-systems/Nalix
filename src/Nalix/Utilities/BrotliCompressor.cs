using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Nalix.Utilities;

/// <summary>
/// Provides methods for compressing and decompressing payloads using Brotli compression.
/// </summary>
[SkipLocalsInit]
public static class BrotliCompressor
{
    private const int BufferSize = 8192; // 8KB buffer for streaming

    /// <summary>
    /// Compresses the provided data using Brotli compression.
    /// </summary>
    /// <param name="data">The data to compress.</param>
    /// <returns>The compressed data as a byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown when compression fails.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Compress(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty) return [];

        using MemoryStream outputStream = new(data.Length / 2); // Dự đoán kích thước sau nén
        try
        {
            using BrotliStream brotliStream = new(outputStream, CompressionLevel.Optimal, leaveOpen: true);
            brotliStream.Write(data.Span);
            brotliStream.Flush();
            return outputStream.ToArray();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new InvalidOperationException("Compression failed.", ex);
        }
    }

    /// <summary>
    /// Decompresses the provided Brotli compressed data.
    /// </summary>
    /// <param name="compressedData">The compressed data to decompress.</param>
    /// <returns>The decompressed data as a byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown when decompression fails or data is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Decompress(ReadOnlyMemory<byte> compressedData)
    {
        if (compressedData.IsEmpty) return [];

        using MemoryStream inputStream = new(compressedData.Length);
        inputStream.Write(compressedData.Span);
        inputStream.Position = 0;

        using MemoryStream outputStream = new(compressedData.Length * 2); // Dự đoán kích thước sau giải nén
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            using BrotliStream brotliStream = new(inputStream, CompressionMode.Decompress);
            int bytesRead;
            while ((bytesRead = brotliStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }
            return outputStream.ToArray();
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException("Invalid Brotli compressed data.", ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new InvalidOperationException("Decompression failed.", ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
