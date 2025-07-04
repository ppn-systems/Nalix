using Nalix.Cryptography.Hashing;
using System;
using Xunit;

namespace Nalix.Test.Cryptography.Hashing;

public class SHA256Tests
{
    [Fact]
    public void ComputeHash_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash = Convert.FromHexString("DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F");

        using SHA256 sha256 = new();

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
        byte[] expectedHash = Convert.FromHexString("DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F");

        // Act
        byte[] actualHash = SHA256.HashData(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void ComputeHash_EmptyInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = [];
        byte[] expectedHash = Convert.FromHexString("DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F");

        using SHA256 sha256 = new();

        // Act
        byte[] actualHash = sha256.ComputeHash(input);

        // Assert
        Assert.False(expectedHash.Equals(actualHash));
    }

    [Fact]
    public void FinalizeHash_CalledTwice_ShouldReturnSameHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        using SHA256 sha256 = new();

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
        byte[] expectedHash = Convert.FromHexString("DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F");

        using SHA256 sha256 = new();

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
        SHA256 sha256 = new();
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
        SHA256 sha256 = new();
        sha256.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sha256.ComputeHash([1, 2, 3]));
    }
}