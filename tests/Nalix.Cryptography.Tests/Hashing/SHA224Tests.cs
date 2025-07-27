using Nalix.Cryptography.Hashing;
using System;
using Xunit;

namespace Nalix.Cryptography.Tests.Hashing;

public class SHA224Tests
{
    [Fact]
    public void ComputeHash_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash = Convert.FromHexString("72A23DFA411BA6FDE01DBFABF3B00A709C93EBF273DC29E2D8B261FF");

        using SHA224 sha224 = new();

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
        byte[] expectedHash = Convert.FromHexString("72A23DFA411BA6FDE01DBFABF3B00A709C93EBF273DC29E2D8B261FF");

        // Act
        byte[] actualHash = SHA224.HashData(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void ComputeHash_EmptyInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = [];
        byte[] expectedHash = Convert.FromHexString("72A23DFA411BA6FDE01DBFABF3B00A709C93EBF273DC29E2D8B261FF");

        using SHA224 sha224 = new();

        // Act
        byte[] actualHash = sha224.ComputeHash(input);

        // Assert
        Assert.False(expectedHash.Equals(actualHash));
    }

    [Fact]
    public void FinalizeHash_CalledTwice_ShouldReturnSameHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        using SHA224 sha224 = new();

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
        byte[] expectedHash = Convert.FromHexString("72A23DFA411BA6FDE01DBFABF3B00A709C93EBF273DC29E2D8B261FF");

        using SHA224 sha224 = new();

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
        SHA224 sha224 = new();
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
        SHA224 sha224 = new();
        sha224.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => sha224.ComputeHash([1, 2, 3]));
    }
}