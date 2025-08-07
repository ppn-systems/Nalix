using Nalix.Shared.LZ4;
using Nalix.Shared.Memory.Unsafe;
using System;
using System.Text;
using Xunit;

namespace Nalix.Shared.Tests.LZ4;

public class LZ4CodecTests
{
    [Fact]
    public void Compress_EmptyInput_ReturnsHeaderSize()
    {
        // Arrange
        var input = ReadOnlySpan<Byte>.Empty;
        var output = new Byte[LZ4BlockHeader.Size];

        // Act
        Int32 result = LZ4Codec.Encode(input, output);

        // Assert
        Assert.Equal(LZ4BlockHeader.Size, result);
    }

    [Fact]
    public void Compress_EmptyInput_OutputTooSmall_ReturnsMinusOne()
    {
        // Arrange
        var input = ReadOnlySpan<Byte>.Empty;
        var output = new Byte[LZ4BlockHeader.Size - 1]; // Too small for header

        // Act
        Int32 result = LZ4Codec.Encode(input, output);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Decompress_EmptyInput_ReturnsZero()
    {
        // Arrange
        var input = new Byte[LZ4BlockHeader.Size];
        var header = new LZ4BlockHeader(0, LZ4BlockHeader.Size);
        MemOps.WriteUnaligned(input, header);
        var output = Array.Empty<Byte>();

        // Act
        Int32 result = LZ4Codec.Decode(input, output);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Decompress_InputTooSmall_ReturnsMinusOne()
    {
        // Arrange
        var input = new Byte[LZ4BlockHeader.Size - 1]; // Too small for header
        var output = new Byte[10];

        // Act
        Int32 result = LZ4Codec.Decode(input, output);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void CompressAndDecompress_SmallText_MatchesOriginal()
    {
        // Arrange
        String text = "Hello, world! This is a simple test string.";
        Byte[] original = Encoding.UTF8.GetBytes(text);
        Byte[] compressed = new Byte[1024]; // Large enough for any small input
        Byte[] decompressed = new Byte[original.Length];

        // Act
        Int32 compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize); // Resize to actual compressed size
        Int32 decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void CompressAndDecompress_RepeatedPattern_HighCompressionRatio()
    {
        // Arrange - Create data with high redundancy (good for compression)
        Byte[] original = Encoding.UTF8.GetBytes(new String('A', 1000));
        Byte[] compressed = new Byte[1024];
        Byte[] decompressed = new Byte[original.Length];

        // Act
        Int32 compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);
        Int32 decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.True(compressedSize < original.Length); // Should compress well
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);

        // Additional assertion to verify good compression ratio
        Double compressionRatio = (Double)compressedSize / original.Length;
        Assert.True(compressionRatio < 0.1); // Should get at least 10:1 compression for repeated data
    }

    [Fact]
    public void CompressAndDecompress_MixedContent_MatchesOriginal()
    {
        // Arrange - Create data with mix of text, binary, and repeated patterns
        var builder = new StringBuilder();
        // Add some regular text
        _ = builder.Append("This is a test with mixed content. ");
        // Add some repeated text for good compression
        _ = builder.Append(new String('X', 100));
        // Add some binary-like data
        for (Byte i = 0; i < 50; i++)
        {
            _ = builder.Append((Char)i);
        }

        Byte[] original = Encoding.UTF8.GetBytes(builder.ToString());
        Byte[] compressed = new Byte[original.Length * 2]; // Ensure enough space
        Byte[] decompressed = new Byte[original.Length];

        // Act
        Int32 compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);
        Int32 decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Compress_OutputBufferTooSmall_ReturnsMinusOne()
    {
        // Arrange
        Byte[] original = Encoding.UTF8.GetBytes("This is a test string that won't fit in a tiny buffer");
        // Create a buffer that's definitely too small (even smaller than input)
        Byte[] compressed = new Byte[10];

        // Act
        Int32 result = LZ4Codec.Encode(original, compressed);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Decompress_WrongOutputSize_ReturnsMinusOne()
    {
        // Arrange
        String text = "Original text for compression";
        Byte[] original = Encoding.UTF8.GetBytes(text);
        Byte[] compressed = new Byte[1024];

        Int32 compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);

        // Create output buffer with wrong size (too big)
        Byte[] decompressed = new Byte[original.Length + 10];

        // Act
        Int32 result = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Decompress_CorruptedHeader_ReturnsMinusOne()
    {
        // Arrange
        String text = "Text to compress";
        Byte[] original = Encoding.UTF8.GetBytes(text);
        Byte[] compressed = new Byte[1024];

        Int32 compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);

        // Corrupt the header by changing the original length
        var corruptedHeader = new LZ4BlockHeader(original.Length + 100, compressed.Length);
        MemOps.WriteUnaligned(compressed, corruptedHeader);

        Byte[] decompressed = new Byte[original.Length];

        // Act
        Int32 result = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void CompressAndDecompress_RandomData_MatchesOriginal()
    {
        // Arrange - Create random data (typically harder to compress)
        var random = new Random(42); // Fixed seed for reproducibility
        Byte[] original = new Byte[5000];
        random.NextBytes(original);

        Byte[] compressed = new Byte[original.Length * 2]; // Ensure enough space
        Byte[] decompressed = new Byte[original.Length];

        // Act
        Int32 compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);
        Int32 decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void CompressAndDecompress_LargeData_MatchesOriginal()
    {
        // Arrange - Create large data with some patterns
        const Int32 size = 1_000_000; // 1MB
        Byte[] original = new Byte[size];

        // Fill with pattern data that should compress well
        for (Int32 i = 0; i < size; i++)
        {
            original[i] = (Byte)(i % 256);
        }

        Byte[] compressed = new Byte[size]; // Might need to be larger for worst case
        Byte[] decompressed = new Byte[size];

        // Act
        Int32 compressedSize = LZ4Codec.Encode(original, compressed);

        // If compression failed due to buffer size, try with larger buffer
        if (compressedSize == -1)
        {
            compressed = new Byte[size * 2];
            compressedSize = LZ4Codec.Encode(original, compressed);
        }

        Assert.True(compressedSize > 0);
        Array.Resize(ref compressed, compressedSize);

        Int32 decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }
}