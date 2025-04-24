using Nalix.Cryptography.Symmetric;
using System;
using System.Text;
using Xunit;

namespace Nalix.Cryptography.Test.Symmetric;

public class BlowfishTests
{
    [Fact]
    public void EncryptDecrypt_ValidKey_ShouldReturnOriginalData()
    {
        // Arrange
        string key = "testkey";
        string originalData = "Hello, World!";
        Blowfish blowfish = new(key);

        // Act
        string encryptedData = blowfish.EncryptToBase64(originalData);
        string decryptedData = blowfish.DecryptFromBase64(encryptedData);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public void EncryptDecrypt_EmptyData_ShouldReturnEmpty()
    {
        // Arrange
        string key = "testkey";
        string originalData = string.Empty;
        Blowfish blowfish = new(key);

        // Act
        string encryptedData = blowfish.EncryptToBase64(originalData);
        string decryptedData = blowfish.DecryptFromBase64(encryptedData);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public void EncryptDecrypt_WithDifferentKeys_ShouldFail()
    {
        // Arrange
        string key1 = "key1";
        string key2 = "key2";
        string originalData = "Hello, World!";
        Blowfish blowfishEncrypt = new(key1);
        Blowfish blowfishDecrypt = new(key2);

        // Act
        string encryptedData = blowfishEncrypt.EncryptToBase64(originalData);

        // Assert
        Assert.ThrowsAny<Exception>(() =>
        {
            blowfishDecrypt.DecryptFromBase64(encryptedData);
        });
    }

    [Fact]
    public void EncryptDecrypt_DataLengthNotMultipleOf8_ShouldThrowException()
    {
        // Arrange
        string key = "testkey";
        byte[] originalData = Encoding.ASCII.GetBytes("InvalidLength"); // Length is not a multiple of 8
        Blowfish blowfish = new(key);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => blowfish.EncryptInPlace(originalData, originalData.Length));
    }

    [Fact]
    public void Constructor_InvalidKeyLength_ShouldThrowException()
    {
        // Arrange
        byte[] tooShortKey = new byte[3]; // Key length less than 4
        byte[] tooLongKey = new byte[57]; // Key length more than 56

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Blowfish(tooShortKey));
        Assert.Throws<ArgumentException>(() => new Blowfish(tooLongKey));
    }

    [Fact]
    public void Constructor_ValidKeyLength_ShouldInitializeCorrectly()
    {
        // Arrange
        byte[] validKey = Encoding.ASCII.GetBytes("testkey");

        // Act
        Blowfish blowfish = new(validKey);

        // Assert
        Assert.NotNull(blowfish);
    }

    [Fact]
    public void EncryptDecrypt_NonAsciiCharacters_ShouldWorkCorrectly()
    {
        // Arrange
        string key = "testkey";
        string originalData = "こんにちは世界"; // "Hello, World!" in Japanese
        Blowfish blowfish = new(key);

        // Act
        string encryptedData = blowfish.EncryptToBase64(originalData);
        string decryptedData = blowfish.DecryptFromBase64(encryptedData);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }
}
