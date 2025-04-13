using Notio.Cryptography.Hashing;
using System;
using Xunit;

namespace Notio.Cryptography.Test.Hashing;

public class SHA1Tests
{
    [Fact]
    public void ComputeHash_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash = [
            0x2e, 0xf7, 0xbd, 0xe6, 0x08, 0xce, 0x54, 0x04,
            0xe9, 0x7d, 0x5f, 0x04, 0x62, 0x4e, 0x92, 0x77,
            0x52, 0x7f, 0x70, 0x37
        ];

        using Sha1 sha1 = new();

        // Act
        byte[] actualHash = sha1.ComputeHash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void HashData_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash = [
            0x2e, 0xf7, 0xbd, 0xe6, 0x08, 0xce, 0x54, 0x04,
            0xe9, 0x7d, 0x5f, 0x04, 0x62, 0x4e, 0x92, 0x77,
            0x52, 0x7f, 0x70, 0x37
        ];

        // Act
        byte[] actualHash = Sha1.HashData(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void ComputeHash_EmptyInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = [];
        byte[] expectedHash = [
            0xda, 0x39, 0xa3, 0xee, 0x5e, 0x6b, 0x4b, 0x0d,
            0x32, 0x55, 0xbf, 0xef, 0x95, 0x60, 0x18, 0x90,
            0xaf, 0xd8, 0x07, 0x09
        ];

        using Sha1 sha1 = new();

        // Act
        byte[] actualHash = sha1.ComputeHash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void UpdateAndFinalizeHash_PartialUpdates_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] part1 = System.Text.Encoding.UTF8.GetBytes("Hello, ");
        byte[] part2 = System.Text.Encoding.UTF8.GetBytes("World!");
        byte[] expectedHash = [
            0x2e, 0xf7, 0xbd, 0xe6, 0x08, 0xce, 0x54, 0x04,
            0xe9, 0x7d, 0x5f, 0x04, 0x62, 0x4e, 0x92, 0x77,
            0x52, 0x7f, 0x70, 0x37
        ];

        using Sha1 sha1 = new();

        // Act
        sha1.Update(part1);
        sha1.Update(part2);
        byte[] actualHash = sha1.FinalizeHash();

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void FinalizeHash_CalledTwice_ShouldReturnSameHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        using Sha1 sha1 = new();

        // Act
        sha1.Update(input);
        byte[] firstHash = sha1.FinalizeHash();
        byte[] secondHash = sha1.FinalizeHash();

        // Assert
        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void Dispose_ShouldClearSensitiveData()
    {
        // Arrange
        Sha1 sha1 = new();
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        sha1.Update(input);
        sha1.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => sha1.ComputeHash(input));
    }

    [Fact]
    public void ComputeHash_DisposedInstance_ShouldThrowException()
    {
        // Arrange
        Sha1 sha1 = new();
        sha1.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sha1.ComputeHash([1, 2, 3]));
    }
}
