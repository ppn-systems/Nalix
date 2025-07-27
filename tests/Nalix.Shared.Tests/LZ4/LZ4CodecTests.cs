using Nalix.Shared.LZ4;
using Nalix.Shared.LZ4.Internal;
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
        var input = ReadOnlySpan<byte>.Empty;
        var output = new byte[Header.Size];

        // Act
        int result = LZ4Codec.Encode(input, output);

        // Assert
        Assert.Equal(Header.Size, result);
    }

    [Fact]
    public void Compress_EmptyInput_OutputTooSmall_ReturnsMinusOne()
    {
        // Arrange
        var input = ReadOnlySpan<byte>.Empty;
        var output = new byte[Header.Size - 1]; // Too small for header

        // Act
        int result = LZ4Codec.Encode(input, output);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Decompress_EmptyInput_ReturnsZero()
    {
        // Arrange
        var input = new byte[Header.Size];
        var header = new Header(0, Header.Size);
        MemOps.WriteUnaligned(input, header);
        var output = Array.Empty<byte>();

        // Act
        int result = LZ4Codec.Decode(input, output);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Decompress_InputTooSmall_ReturnsMinusOne()
    {
        // Arrange
        var input = new byte[Header.Size - 1]; // Too small for header
        var output = new byte[10];

        // Act
        int result = LZ4Codec.Decode(input, output);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void CompressAndDecompress_SmallText_MatchesOriginal()
    {
        // Arrange
        string text = "Hello, world! This is a simple test string.";
        byte[] original = Encoding.UTF8.GetBytes(text);
        byte[] compressed = new byte[1024]; // Large enough for any small input
        byte[] decompressed = new byte[original.Length];

        // Act
        int compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize); // Resize to actual compressed size
        int decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void CompressAndDecompress_RepeatedPattern_HighCompressionRatio()
    {
        // Arrange - Create data with high redundancy (good for compression)
        byte[] original = Encoding.UTF8.GetBytes(new string('A', 1000));
        byte[] compressed = new byte[1024];
        byte[] decompressed = new byte[original.Length];

        // Act
        int compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);
        int decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.True(compressedSize < original.Length); // Should compress well
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);

        // Additional assertion to verify good compression ratio
        double compressionRatio = (double)compressedSize / original.Length;
        Assert.True(compressionRatio < 0.1); // Should get at least 10:1 compression for repeated data
    }

    [Fact]
    public void CompressAndDecompress_MixedContent_MatchesOriginal()
    {
        // Arrange - Create data with mix of text, binary, and repeated patterns
        var builder = new StringBuilder();
        // Add some regular text
        builder.Append("This is a test with mixed content. ");
        // Add some repeated text for good compression
        builder.Append(new string('X', 100));
        // Add some binary-like data
        for (byte i = 0; i < 50; i++)
        {
            builder.Append((char)i);
        }

        byte[] original = Encoding.UTF8.GetBytes(builder.ToString());
        byte[] compressed = new byte[original.Length * 2]; // Ensure enough space
        byte[] decompressed = new byte[original.Length];

        // Act
        int compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);
        int decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Compress_OutputBufferTooSmall_ReturnsMinusOne()
    {
        // Arrange
        byte[] original = Encoding.UTF8.GetBytes("This is a test string that won't fit in a tiny buffer");
        // Create a buffer that's definitely too small (even smaller than input)
        byte[] compressed = new byte[10];

        // Act
        int result = LZ4Codec.Encode(original, compressed);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Decompress_WrongOutputSize_ReturnsMinusOne()
    {
        // Arrange
        string text = "Original text for compression";
        byte[] original = Encoding.UTF8.GetBytes(text);
        byte[] compressed = new byte[1024];

        int compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);

        // Create output buffer with wrong size (too big)
        byte[] decompressed = new byte[original.Length + 10];

        // Act
        int result = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Decompress_CorruptedHeader_ReturnsMinusOne()
    {
        // Arrange
        string text = "Text to compress";
        byte[] original = Encoding.UTF8.GetBytes(text);
        byte[] compressed = new byte[1024];

        int compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);

        // Corrupt the header by changing the original length
        var corruptedHeader = new Header(original.Length + 100, compressed.Length);
        MemOps.WriteUnaligned(compressed, corruptedHeader);

        byte[] decompressed = new byte[original.Length];

        // Act
        int result = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void CompressAndDecompress_RandomData_MatchesOriginal()
    {
        // Arrange - Create random data (typically harder to compress)
        var random = new Random(42); // Fixed seed for reproducibility
        byte[] original = new byte[5000];
        random.NextBytes(original);

        byte[] compressed = new byte[original.Length * 2]; // Ensure enough space
        byte[] decompressed = new byte[original.Length];

        // Act
        int compressedSize = LZ4Codec.Encode(original, compressed);
        Array.Resize(ref compressed, compressedSize);
        int decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.True(compressedSize > 0);
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void CompressAndDecompress_LargeData_MatchesOriginal()
    {
        // Arrange - Create large data with some patterns
        const int size = 1_000_000; // 1MB
        byte[] original = new byte[size];

        // Fill with pattern data that should compress well
        for (int i = 0; i < size; i++)
        {
            original[i] = (byte)(i % 256);
        }

        byte[] compressed = new byte[size]; // Might need to be larger for worst case
        byte[] decompressed = new byte[size];

        // Act
        int compressedSize = LZ4Codec.Encode(original, compressed);

        // If compression failed due to buffer size, try with larger buffer
        if (compressedSize == -1)
        {
            compressed = new byte[size * 2];
            compressedSize = LZ4Codec.Encode(original, compressed);
        }

        Assert.True(compressedSize > 0);
        Array.Resize(ref compressed, compressedSize);

        int decompressedSize = LZ4Codec.Decode(compressed, decompressed);

        // Assert
        Assert.Equal(original.Length, decompressedSize);
        Assert.Equal(original, decompressed);
    }
}