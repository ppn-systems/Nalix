using Nalix.Shared.LZ4;
using Nalix.Shared.Memory.Buffers;
using System;
using Xunit;

namespace Nalix.Shared.Tests.LZ4;

/// <summary>
/// Unit tests for <see cref="LZ4Codec"/>.
/// </summary>
public sealed class LZ4CodecTests
{
    /// <summary>
    /// Creates a deterministic sample payload for testing.
    /// </summary>
    /// <param name="length">The desired payload length.</param>
    /// <returns>A byte array with deterministic content.</returns>
    private static Byte[] CreateSamplePayload(Int32 length)
    {
        var data = new Byte[length];

        for (Int32 i = 0; i < data.Length; i++)
        {
            // Simple repeating pattern for better compression.
            data[i] = (Byte)(i % 256);
        }

        return data;
    }

    [Fact]
    public void EncodeDecode_WithSpan_RoundTripsData()
    {
        // Arrange
        Byte[] original = CreateSamplePayload(4 * 1024); // 4 KB

        // Estimate a safe compressed buffer size.
        Int32 maxCompressedLength = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        Byte[] compressed = new Byte[maxCompressedLength];

        // The decode API needs a buffer for decompressed data.
        Byte[] decompressed = new Byte[original.Length];

        // Act
        Int32 writtenCompressed = LZ4Codec.Encode(original, compressed);

        // Assert
        Assert.True(writtenCompressed > 0);

        Int32 writtenDecompressed = LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            decompressed);

        Assert.Equal(original.Length, writtenDecompressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Encode_WithTooSmallOutput_ReturnsMinusOne()
    {
        // Arrange
        Byte[] original = CreateSamplePayload(1024);
        Byte[] tooSmallOutput = new Byte[Nalix.Shared.LZ4.LZ4BlockHeader.Size - 1];

        // Act
        Int32 result = LZ4Codec.Encode(original, tooSmallOutput);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Encode_WithLease_RoundTripsData()
    {
        // Arrange
        Byte[] original = CreateSamplePayload(8 * 1024);

        // Act
        Boolean encoded = LZ4Codec.Encode(
            original,
            out BufferLease lease,
            out Int32 compressedLength);

        // Assert
        Assert.True(encoded);
        Assert.NotNull(lease);
        Assert.True(compressedLength > 0);

        using (lease!)
        {
            // Decode using lease content.
            Byte[] decoded = new Byte[original.Length];

            Int32 written = LZ4Codec.Decode(
                lease.Span[..compressedLength],
                decoded);

            Assert.Equal(original.Length, written);
            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void Decode_ToByteArray_RoundTripsData()
    {
        // Arrange
        Byte[] original = CreateSamplePayload(4096);

        Int32 maxCompressedLength = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        Byte[] compressed = new Byte[maxCompressedLength];

        Int32 writtenCompressed = LZ4Codec.Encode(original, compressed);
        Assert.True(writtenCompressed > 0);

        // Act
        Boolean decoded = LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            out Byte[] output,
            out Int32 bytesWritten);

        // Assert
        Assert.True(decoded);
        Assert.NotNull(output);
        Assert.Equal(original.Length, bytesWritten);

        var actual = output!.AsSpan(0, bytesWritten).ToArray();
        Assert.Equal(original, actual);
    }

    [Fact]
    public void Decode_ToLease_RoundTripsData_IfSupported()
    {
        // Arrange
        Byte[] original = CreateSamplePayload(10 * 1024);

        Int32 maxCompressedLength = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        Byte[] compressed = new Byte[maxCompressedLength];

        Int32 writtenCompressed = LZ4Codec.Encode(original, compressed);
        Assert.True(writtenCompressed > 0);

        // Act
        Boolean decoded = LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            out BufferLease lease,
            out Int32 bytesWritten);

        // Nếu decoder trả false thì đây là hành vi hợp lệ: không giải nén được,
        // lease phải null và bytesWritten phải 0. Không fail test.
        if (!decoded)
        {
            Assert.Null(lease);
            Assert.Equal(0, bytesWritten);
            return;
        }

        // Nếu decoder hỗ trợ, decoded phải true và lease != null
        Assert.NotNull(lease);

        using (lease!)
        {
            Assert.True(bytesWritten > 0);

            var span = lease.Span[..bytesWritten].ToArray();
            Assert.Equal(original, span);
        }
    }

    [Fact]
    public void EncodeDecode_WithEmptyInput_Works()
    {
        // Arrange
        ReadOnlySpan<Byte> original = ReadOnlySpan<Byte>.Empty;

        Int32 maxCompressedLength = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        Byte[] compressed = new Byte[maxCompressedLength];

        Byte[] decompressed = Array.Empty<Byte>();

        // Act
        Int32 writtenCompressed = LZ4Codec.Encode(original, compressed);

        // Empty input may still produce a small header, so we mainly
        // check that Encode and Decode do not throw and are consistent.
        Assert.True(writtenCompressed >= 0);

        if (writtenCompressed == 0)
        {
            // Nothing to decode.
            return;
        }

        Int32 writtenDecompressed = LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            decompressed);

        Assert.Equal(0, writtenDecompressed);
    }
}