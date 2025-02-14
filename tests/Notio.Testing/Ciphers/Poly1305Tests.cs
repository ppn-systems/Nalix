using Notio.Cryptography.Ciphers.Symmetric;
using System;
using Xunit;

namespace Notio.Testing.Ciphers;

public class Poly1305Tests
{
    [Fact]
    public void ComputeTag_ValidKeyAndMessage_ReturnsExpectedLength()
    {
        byte[] key =
        [
        0x85, 0xd6, 0xbe, 0x78, 0x57, 0x55, 0x6d, 0x33,
        0x7f, 0x44, 0x52, 0xfe, 0x42, 0xd5, 0x06, 0xa8,
        0x01, 0x03, 0x80, 0x08, 0x90, 0x3c, 0xe0, 0x17,
        0x5c, 0x23, 0x76, 0xa8, 0x94, 0x7c, 0x73, 0x1a
        ];
        byte[] message = "Hello, World!"u8.ToArray();

        Poly1305 poly = new(key);
        byte[] tag = poly.ComputeTag(message);

        Assert.Equal(16, tag.Length);
    }

    [Fact]
    public void ComputeTag_EmptyMessage_ReturnsValidTag()
    {
        byte[] key = new byte[32]; // All zeros
        byte[] message = []; // Empty message

        Poly1305 poly = new(key);
        byte[] tag = poly.ComputeTag(message);

        Assert.Equal(16, tag.Length);
    }

    [Fact]
    public void ComputeTag_InvalidKey_ThrowsException()
    {
        byte[] key = new byte[31]; // Invalid length (should be 32)
        byte[] message = [0x01, 0x02, 0x03];

        Assert.Throws<ArgumentException>(() => new Poly1305(key));
    }

    [Fact]
    public void Compute_StaticMethod_ReturnsSameResult()
    {
        byte[] key =
        [
        0x85, 0xd6, 0xbe, 0x78, 0x57, 0x55, 0x6d, 0x33,
        0x7f, 0x44, 0x52, 0xfe, 0x42, 0xd5, 0x06, 0xa8,
        0x01, 0x03, 0x80, 0x08, 0x90, 0x3c, 0xe0, 0x17,
        0x5c, 0x23, 0x76, 0xa8, 0x94, 0x7c, 0x73, 0x1a
        ];
        byte[] message = "Hello, World!"u8.ToArray();

        byte[] tag1 = Poly1305.Compute(key, message);
        Poly1305 poly = new(key);
        byte[] tag2 = poly.ComputeTag(message);

        Assert.Equal(tag1, tag2);
    }
}
