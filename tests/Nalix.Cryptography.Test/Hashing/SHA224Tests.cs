using Nalix.Cryptography.Hashing;
using System;
using Xunit;

namespace Nalix.Cryptography.Test.Hashing;

public class SHA224Tests
{
    [Fact]
    public void ComputeHash_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash = [
            0x45, 0xa5, 0x0f, 0x0b, 0x3e, 0x5c, 0x0c, 0x17,
            0xd9, 0x4a, 0x84, 0x51, 0xc1, 0x3d, 0x36, 0x5c,
            0x54, 0x3e, 0x62, 0x3a, 0xd0, 0x5c, 0x06, 0x9a,
            0x98, 0xa2, 0x1b, 0x63
        ];

        using Sha224 sha224 = new();

        // Act
        byte[] actualHash = sha224.ComputeHash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void HashData_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash = [
            0x45, 0xa5, 0x0f, 0x0b, 0x3e, 0x5c, 0x0c, 0x17,
            0xd9, 0x4a, 0x84, 0x51, 0xc1, 0x3d, 0x36, 0x5c,
            0x54, 0x3e, 0x62, 0x3a, 0xd0, 0x5c, 0x06, 0x9a,
            0x98, 0xa2, 0x1b, 0x63
        ];

        // Act
        byte[] actualHash = Sha224.HashData(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void ComputeHash_EmptyInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = [];
        byte[] expectedHash = [
            0xd1, 0x4a, 0x02, 0x8c, 0x2a, 0x3a, 0x2b, 0xc9,
            0x47, 0x61, 0x02, 0xbb, 0x28, 0x82, 0x34, 0xc4,
            0x15, 0xa2, 0xb0, 0x1f, 0x82, 0x8e, 0xa6, 0x2a,
            0xc5, 0xb3, 0xe4, 0x2f
        ];

        using Sha224 sha224 = new();

        // Act
        byte[] actualHash = sha224.ComputeHash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void FinalizeHash_CalledTwice_ShouldReturnSameHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        using Sha224 sha224 = new();

        // Act
        sha224.Update(input);
        byte[] firstHash = sha224.FinalizeHash();
        byte[] secondHash = sha224.FinalizeHash();

        // Assert
        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void UpdateAndFinalizeHash_PartialUpdates_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] part1 = System.Text.Encoding.UTF8.GetBytes("Hello, ");
        byte[] part2 = System.Text.Encoding.UTF8.GetBytes("World!");
        byte[] expectedHash = [
            0x45, 0xa5, 0x0f, 0x0b, 0x3e, 0x5c, 0x0c, 0x17,
            0xd9, 0x4a, 0x84, 0x51, 0xc1, 0x3d, 0x36, 0x5c,
            0x54, 0x3e, 0x62, 0x3a, 0xd0, 0x5c, 0x06, 0x9a,
            0x98, 0xa2, 0x1b, 0x63
        ];

        using Sha224 sha224 = new();

        // Act
        sha224.Update(part1);
        sha224.Update(part2);
        byte[] actualHash = sha224.FinalizeHash();

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void Dispose_ShouldClearSensitiveData()
    {
        // Arrange
        Sha224 sha224 = new();
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        sha224.Update(input);
        sha224.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => sha224.ComputeHash(input));
    }

    [Fact]
    public void ComputeHash_DisposedInstance_ShouldThrowException()
    {
        // Arrange
        Sha224 sha224 = new();
        sha224.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sha224.ComputeHash([1, 2, 3]));
    }
}
