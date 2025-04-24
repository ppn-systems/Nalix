using Nalix.Cryptography.Hashing;
using System;
using Xunit;

namespace Nalix.Cryptography.Test.Hashing;

public class SHA256Tests
{
    [Fact]
    public void ComputeHash_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash =
        [
            0xc0, 0x53, 0x5e, 0x4b, 0xf6, 0x93, 0x70, 0x7d,
            0xf9, 0x9c, 0x3e, 0x1d, 0x95, 0x97, 0x47, 0x6e,
            0x2e, 0x84, 0x6e, 0x0e, 0x04, 0x3d, 0x0e, 0x6a,
            0x77, 0xfd, 0x7f, 0x6e, 0x8e, 0x6f, 0xe5, 0xc1
        ];

        using Sha256 sha256 = new();

        // Act
        byte[] actualHash = sha256.ComputeHash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void HashData_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash =
        [
            0xc0, 0x53, 0x5e, 0x4b, 0xf6, 0x93, 0x70, 0x7d,
            0xf9, 0x9c, 0x3e, 0x1d, 0x95, 0x97, 0x47, 0x6e,
            0x2e, 0x84, 0x6e, 0x0e, 0x04, 0x3d, 0x0e, 0x6a,
            0x77, 0xfd, 0x7f, 0x6e, 0x8e, 0x6f, 0xe5, 0xc1
        ];

        // Act
        byte[] actualHash = Sha256.HashData(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void ComputeHash_EmptyInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = [];
        byte[] expectedHash =
        [
            0xe3, 0xb0, 0xc4, 0x42, 0x98, 0xfc, 0x1c, 0x14,
            0x9a, 0xfb, 0xf4, 0xc8, 0x99, 0x6f, 0xb9, 0x24,
            0x27, 0xae, 0x41, 0xe4, 0x64, 0x9b, 0x93, 0x4c,
            0xa4, 0x95, 0x99, 0x1b, 0x78, 0x52, 0xb8, 0x55
        ];

        using Sha256 sha256 = new();

        // Act
        byte[] actualHash = sha256.ComputeHash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void FinalizeHash_CalledTwice_ShouldReturnSameHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        using Sha256 sha256 = new();

        // Act
        sha256.Update(input);
        byte[] firstHash = sha256.FinalizeHash();
        byte[] secondHash = sha256.FinalizeHash();

        // Assert
        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void UpdateAndFinalizeHash_PartialUpdates_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] part1 = System.Text.Encoding.UTF8.GetBytes("Hello, ");
        byte[] part2 = System.Text.Encoding.UTF8.GetBytes("World!");
        byte[] expectedHash =
        [
            0xc0, 0x53, 0x5e, 0x4b, 0xf6, 0x93, 0x70, 0x7d,
            0xf9, 0x9c, 0x3e, 0x1d, 0x95, 0x97, 0x47, 0x6e,
            0x2e, 0x84, 0x6e, 0x0e, 0x04, 0x3d, 0x0e, 0x6a,
            0x77, 0xfd, 0x7f, 0x6e, 0x8e, 0x6f, 0xe5, 0xc1
        ];

        using Sha256 sha256 = new();

        // Act
        sha256.Update(part1);
        sha256.Update(part2);
        byte[] actualHash = sha256.FinalizeHash();

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void Dispose_ShouldClearSensitiveData()
    {
        // Arrange
        Sha256 sha256 = new();
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        sha256.Update(input);
        sha256.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => sha256.ComputeHash(input));
    }

    [Fact]
    public void ComputeHash_DisposedInstance_ShouldThrowException()
    {
        // Arrange
        Sha256 sha256 = new();
        sha256.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sha256.ComputeHash([1, 2, 3]));
    }
}
