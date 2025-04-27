using Nalix.Shared.LZ4.Engine;

namespace Nalix.Shared.LZ4;

/// <summary>
/// Provides high-performance methods for compressing and decompressing data using the Nalix LZ4 algorithm.
/// This class is static-like and designed for zero-allocation workflows.
/// </summary>
public class LZ4Codec
{
    /// <summary>
    /// Compresses the input data into the specified output buffer.
    /// </summary>
    /// <param name="input">The input data to compress.</param>
    /// <param name="output">The output buffer to receive the compressed data.</param>
    /// <returns>The number of bytes written to the output buffer, or -1 if compression fails.</returns>
    public static int Encode(System.ReadOnlySpan<byte> input, System.Span<byte> output)
        => LZ4Encoder.Encode(input, output);

    /// <summary>
    /// Compresses the input byte array into the specified output byte array.
    /// </summary>
    /// <param name="input">The input byte array to compress.</param>
    /// <param name="output">The output byte array to receive the compressed data.</param>
    /// <returns>The number of bytes written to the output array, or -1 if compression fails.</returns>
    public static int Encode(byte[] input, byte[] output)
        => LZ4Encoder.Encode(System.MemoryExtensions.AsSpan(input), System.MemoryExtensions.AsSpan(output));

    /// <summary>
    /// Decompresses the input compressed data into the specified output buffer.
    /// </summary>
    /// <param name="input">The compressed input data, including header information.</param>
    /// <param name="output">The output buffer to receive the decompressed data.</param>
    /// <returns>The number of bytes written to the output buffer, or -1 if decompression fails.</returns>
    public static int Decode(System.ReadOnlySpan<byte> input, System.Span<byte> output)
        => LZ4Decoder.Decode(input, output);

    /// <summary>
    /// Decompresses the compressed input byte array into the specified output byte array.
    /// </summary>
    /// <param name="input">The compressed input byte array, including header information.</param>
    /// <param name="output">The output byte array to receive the decompressed data.</param>
    /// <returns>The number of bytes written to the output array, or -1 if decompression fails.</returns>
    public static int Decode(byte[] input, byte[] output)
        => LZ4Decoder.Decode(System.MemoryExtensions.AsSpan(input), System.MemoryExtensions.AsSpan(output));
}
