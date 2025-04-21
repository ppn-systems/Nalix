using Notio.Cryptography.Symmetric;
using System;
using Xunit;

namespace Notio.Cryptography.Test.Symmetric;

public class TwofishTests
{
    [Fact]
    public void ECB_EncryptDecrypt_ValidKeyAndPlaintext_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] plaintext = new byte[16];
        new Random().NextBytes(key);
        new Random().NextBytes(plaintext);

        // Act
        byte[] ciphertext = Twofish.ECB.Encrypt(key, plaintext);
        byte[] decryptedPlaintext = Twofish.ECB.Decrypt(key, ciphertext);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }

    [Fact]
    public void ECB_EncryptDecrypt_InvalidKeyLength_ShouldThrowException()
    {
        // Arrange
        byte[] invalidKey = new byte[15]; // Not 16, 24, or 32 bytes
        byte[] plaintext = new byte[16];
        new Random().NextBytes(invalidKey);
        new Random().NextBytes(plaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Encrypt(invalidKey, plaintext));
    }

    [Fact]
    public void ECB_EncryptDecrypt_InvalidPlaintextLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] invalidPlaintext = new byte[15]; // Not a multiple of 16 bytes
        new Random().NextBytes(key);
        new Random().NextBytes(invalidPlaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Encrypt(key, invalidPlaintext));
    }

    [Fact]
    public void CBC_EncryptDecrypt_ValidKeyAndPlaintext_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] iv = new byte[16];
        byte[] plaintext = new byte[32]; // Two blocks
        new Random().NextBytes(key);
        new Random().NextBytes(iv);
        new Random().NextBytes(plaintext);

        // Act
        byte[] ciphertext = Twofish.CBC.Encrypt(key, iv, plaintext);
        byte[] decryptedPlaintext = Twofish.CBC.Decrypt(key, iv, ciphertext);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }

    [Fact]
    public void CBC_Encrypt_WithoutIV_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] plaintext = new byte[16]; // One block
        new Random().NextBytes(key);
        new Random().NextBytes(plaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.CBC.Encrypt(key, [], plaintext));
    }

    [Fact]
    public void CBC_EncryptDecrypt_InvalidKeyLength_ShouldThrowException()
    {
        // Arrange
        byte[] invalidKey = new byte[15]; // Not 16, 24, or 32 bytes
        byte[] iv = new byte[16];
        byte[] plaintext = new byte[16];
        new Random().NextBytes(invalidKey);
        new Random().NextBytes(iv);
        new Random().NextBytes(plaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.CBC.Encrypt(invalidKey, iv, plaintext));
    }

    [Fact]
    public void CBC_EncryptDecrypt_InvalidIVLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] invalidIV = new byte[15]; // Not 16 bytes
        byte[] plaintext = new byte[16];
        new Random().NextBytes(key);
        new Random().NextBytes(invalidIV);
        new Random().NextBytes(plaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.CBC.Encrypt(key, invalidIV, plaintext));
    }

    [Fact]
    public void CBC_EncryptDecrypt_InvalidPlaintextLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] iv = new byte[16];
        byte[] invalidPlaintext = new byte[15]; // Not a multiple of 16 bytes
        new Random().NextBytes(key);
        new Random().NextBytes(iv);
        new Random().NextBytes(invalidPlaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.CBC.Encrypt(key, iv, invalidPlaintext));
    }

    [Fact]
    public void EncryptDecrypt_LargeData_ShouldWorkCorrectly()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] iv = new byte[16];
        byte[] plaintext = new byte[1024]; // 1 KB of data
        new Random().NextBytes(key);
        new Random().NextBytes(iv);
        new Random().NextBytes(plaintext);

        // Act
        byte[] ciphertext = Twofish.CBC.Encrypt(key, iv, plaintext);
        byte[] decryptedPlaintext = Twofish.CBC.Decrypt(key, iv, ciphertext);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }
}
