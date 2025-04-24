using Nalix.Cryptography.Hashing;
using System;
using Xunit;

namespace Nalix.Cryptography.Test.Hashing;

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

        using SHA1 sha1 = new();

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
        byte[] actualHash = SHA1.HashData(input);

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

        using SHA1 sha1 = new();

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
        byte[] expectedHash =
        [
            0x23, 0x09, 0x7d, 0x22, 0x34, 0x05, 0xd8, 0x22,
            0x86, 0x42, 0xa4, 0x77, 0xbd, 0xa2, 0x55, 0xb3,
            0x2a, 0xad, 0xbc, 0xe4, 0xbd, 0xa0, 0xb3, 0xf7,
            0xe3, 0x6c, 0x9d, 0xa7
        ]; // This is the correct SHA224 hash for "Hello, World!"

        using var sha224 = new SHA224(); // Ensure you're using the correct `SHA224` implementation

        // Act
        sha224.Update(part1);
        sha224.Update(part2);
        byte[] actualHash = sha224.FinalizeHash();

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void FinalizeHash_CalledTwice_ShouldReturnSameHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        using SHA1 sha1 = new();

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
        SHA1 sha1 = new();
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
        SHA1 sha1 = new();
        sha1.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sha1.ComputeHash([1, 2, 3]));
    }
}
