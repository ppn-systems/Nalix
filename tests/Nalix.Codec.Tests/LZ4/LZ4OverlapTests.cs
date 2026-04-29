using System;
using System.Linq;
using Nalix.Codec.LZ4;
using Xunit;

namespace Nalix.Codec.Tests.LZ4;

public class LZ4OverlapTests
{
    [Fact]
    public void DecompressOverlapMatch_Succeeds()
    {
        // Arrange: Create a payload with long repeating pattern (RLE-like)
        // This will force the encoder to use a match with offset = 1 and large length.
        byte[] original = Enumerable.Repeat((byte)'A', 1000).ToArray();
        
        int maxCompressedLength = LZ4BlockEncoder.GetMaxLength(original.Length);
        byte[] compressed = new byte[maxCompressedLength];
        byte[] decompressed = new byte[original.Length];

        // Act
        int writtenCompressed = LZ4Codec.Encode(original, compressed);
        int writtenDecompressed = LZ4Codec.Decode(compressed.AsSpan(0, writtenCompressed), decompressed);

        // Assert
        Assert.Equal(original.Length, writtenDecompressed);
        Assert.Equal(original, decompressed);
    }
}
