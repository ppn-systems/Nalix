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

    private static Byte[] RandomBytes(Int32 length, Int32 seed = 12345)
    {
        var rnd = new Random(seed + length);
        var data = new Byte[length];
        rnd.NextBytes(data);
        return data;
    }

    [Fact]
    public void Encode_Empty_ReturnsHeaderOnly_AndDecodeToEmpty()
    {
        ReadOnlySpan<Byte> input = [];

        // allocate exactly header size (8 bytes) to be strict
        var outBuf = new Byte[8];
        Int32 written = LZ4Codec.Encode(input, outBuf);

        Assert.Equal(8, written); // header only

        // decode into empty output span
        var dest = Array.Empty<Byte>();
        Int32 decoded = LZ4Codec.Decode(outBuf.AsSpan(0, written), dest);
        Assert.Equal(0, decoded);

        // decode using out-array overload
        Boolean ok = LZ4Codec.Decode(outBuf.AsSpan(0, written), out var outArr, out Int32 bytesWritten);
        Assert.True(ok);
        Assert.NotNull(outArr);
        Assert.Equal(0, bytesWritten);
        Assert.Empty(outArr!);
    }

    [Fact]
    public void Roundtrip_SmallLiteralOnly()
    {
        Byte[] input = [1, 2, 3, 4, 5, 6];
        Int32 maxLen = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(input.Length);
        var outBuf = new Byte[maxLen];

        Int32 written = LZ4Codec.Encode(input, outBuf);
        Assert.InRange(written, 8, maxLen); // must include header
        Assert.True(written <= maxLen);

        // decode with exact-sized buffer (original length must match)
        var dest = new Byte[input.Length];
        Int32 decoded = LZ4Codec.Decode(outBuf.AsSpan(0, written), dest);
        Assert.Equal(input.Length, decoded);
        Assert.Equal(input, dest);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(4096)]
    [InlineData(65536)]
    public void Roundtrip_Random_VariousSizes(Int32 size)
    {
        Byte[] input = RandomBytes(size);
        Int32 maxLen = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(size);
        var outBuf = new Byte[maxLen];

        Int32 written = LZ4Codec.Encode(input, outBuf);
        Assert.InRange(written, 8, maxLen);

        // decode span overload
        var dest = new Byte[size];
        Int32 decoded = LZ4Codec.Decode(outBuf.AsSpan(0, written), dest);
        Assert.Equal(size, decoded);
        Assert.Equal(input, dest);

        // decode out-array overload
        Boolean ok = LZ4Codec.Decode(outBuf.AsSpan(0, written), out var outArr, out Int32 bytesWritten);
        Assert.True(ok);
        Assert.Equal(size, bytesWritten);
        Assert.Equal(input, outArr);
    }

    [Fact]
    public void Encode_BufferTooSmall_ReturnsMinusOne()
    {
        Byte[] input = RandomBytes(32);
        // deliberately too small (smaller than 8-byte header)
        var outBuf = new Byte[4];

        Int32 written = LZ4Codec.Encode(input, outBuf);
        Assert.Equal(-1, written);
    }

    [Fact]
    public void Decode_WithWrongOutputSize_ReturnsMinusOne()
    {
        Byte[] input = RandomBytes(1000);
        Int32 maxLen = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(input.Length);
        var outBuf = new Byte[maxLen];

        Int32 written = LZ4Codec.Encode(input, outBuf);
        Assert.True(written > 0);

        // provide output buffer of wrong size
        var destWrong = new Byte[input.Length - 1];
        Int32 decoded = LZ4Codec.Decode(outBuf.AsSpan(0, written), destWrong);
        Assert.Equal(-1, decoded);
    }

    [Fact]
    public void Decode_InvalidHeader_Fails()
    {
        Byte[] input = RandomBytes(128);
        Int32 maxLen = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(input.Length);
        var outBuf = new Byte[maxLen];

        Int32 written = LZ4Codec.Encode(input, outBuf);
        Assert.True(written >= 8);

        // corrupt header: set OriginalLength to a different value
        // header layout: int OriginalLength (offset 0), int CompressedLength (offset 4)
        Span<Byte> slice = outBuf.AsSpan(0, written);
        // toggle one bit in OriginalLength
        slice[0] ^= 0xFF;

        // span overload should return -1
        var dest = new Byte[input.Length];
        Int32 decoded = LZ4Codec.Decode(slice, dest);
        Assert.Equal(-1, decoded);

        // out-array overload should return false
        Boolean ok = LZ4Codec.Decode(slice, out var outArr, out Int32 bytesWritten);
        Assert.False(ok);
        Assert.Null(outArr);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Encode_ArrayOverload_Works()
    {
        Byte[] input = RandomBytes(1024);
        Int32 maxLen = Nalix.Shared.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(input.Length);
        var outBuf = new Byte[maxLen];

        Int32 written = LZ4Codec.Encode(input, outBuf);
        Assert.True(written > 0);

        // now use array overloads for both directions
        var dest = new Byte[input.Length];
        Int32 decoded = LZ4Codec.Decode(outBuf, dest);
        Assert.Equal(input.Length, decoded);
        Assert.Equal(input, dest);
    }

    [Fact]
    public void Encode_ConvenienceAlloc_ReturnsTightBuffer()
    {
        Byte[] input = RandomBytes(777);
        Byte[] compressed = LZ4Codec.Encode(input);

        // decode back via out-array overload (no pre-alloc)
        Boolean ok = LZ4Codec.Decode(compressed, out var outArr, out Int32 written);
        Assert.True(ok);
        Assert.NotNull(outArr);
        Assert.Equal(input.Length, written);
        Assert.Equal(input, outArr);
    }
}