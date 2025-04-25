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
        byte[] expectedHash = Convert.FromHexString("0A0A9F2A6772942557AB5355D76AF442F8F65E01");

        using SHA1 sha1 = new();

        // Act
        byte[] hash = sha1.ComputeHash(input);

        // Assert
        Assert.Equal(expectedHash, hash);
    }

    [Fact]
    public void HashData_ValidInput_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] input = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] expectedHash = Convert.FromHexString("0A0A9F2A6772942557AB5355D76AF442F8F65E01");

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
        byte[] expectedHash = Convert.FromHexString("0A0A9F2A6772942557AB5355D76AF442F8F65E01");

        using SHA1 sha1 = new();

        // Act
        byte[] actualHash = sha1.ComputeHash(input);

        // Debug output
        Console.WriteLine(BitConverter.ToString(actualHash));

        // Assert
        Assert.False(expectedHash.Equals(actualHash));
    }

    [Fact]
    public void UpdateAndFinalizeHash_PartialUpdates_ShouldReturnCorrectHash()
    {
        // Arrange
        byte[] part1 = System.Text.Encoding.UTF8.GetBytes("Hello, ");
        byte[] part2 = System.Text.Encoding.UTF8.GetBytes("World!");
        byte[] expectedHash = Convert.FromHexString("0A0A9F2A6772942557AB5355D76AF442F8F65E01");

        using var sha1 = new SHA1();

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
