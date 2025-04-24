using Nalix.Cryptography.Symmetric;
using System;
using Xunit;

namespace Nalix.Cryptography.Test.Symmetric;

public class XteaTests
{
    [Fact]
    public void EncryptDecrypt_ValidKeyAndPlaintext_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, XTEA!");
        new Random().NextBytes(key);

        // Act
        byte[] ciphertext = Xtea.Encrypt(plaintext, key);
        byte[] decryptedPlaintext = Xtea.Decrypt(ciphertext, key);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }

    [Fact]
    public void EncryptDecrypt_WithIV_ShouldWorkCorrectly()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] iv = new byte[Xtea.BlockSizeInBytes];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, CBC Mode!");
        new Random().NextBytes(key);
        new Random().NextBytes(iv);

        // Act
        byte[] ciphertext = Xtea.Encrypt(plaintext, key, iv);
        byte[] decryptedPlaintext = Xtea.Decrypt(ciphertext, key, iv);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }

    [Fact]
    public void EncryptDecrypt_WithPadding_ShouldHandlePaddedDataCorrectly()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Non-multiple of block size");
        new Random().NextBytes(key);

        // Act
        byte[] ciphertext = Xtea.Encrypt(plaintext, key);
        byte[] decryptedPlaintext = Xtea.Decrypt(ciphertext, key);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }

    [Fact]
    public void EncryptDecrypt_InvalidKeyLength_ShouldThrowException()
    {
        // Arrange
        byte[] invalidKey = new byte[Xtea.KeySizeInBytes - 1]; // Key is too short
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Invalid key test");
        new Random().NextBytes(invalidKey);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(plaintext, invalidKey));
    }

    [Fact]
    public void EncryptDecrypt_InvalidIVLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] invalidIV = new byte[Xtea.BlockSizeInBytes - 1]; // IV is too short
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Invalid IV test");
        new Random().NextBytes(key);
        new Random().NextBytes(invalidIV);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(plaintext, key, invalidIV));
    }

    [Fact]
    public void Decrypt_InvalidPadding_ShouldThrowCryptoException()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Invalid padding test");
        new Random().NextBytes(key);

        byte[] ciphertext = Xtea.Encrypt(plaintext, key);

        // Corrupt the padding bytes in the ciphertext
        ciphertext[^1] = 0xFF;

        // Act & Assert
        Assert.Throws<Nalix.Common.Exceptions.CryptoException>(() => Xtea.Decrypt(ciphertext, key));
    }

    [Fact]
    public void EncryptDecrypt_LargeData_ShouldWorkCorrectly()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = new byte[1024 * 1024]; // 1 MB of data
        new Random().NextBytes(key);
        new Random().NextBytes(plaintext);

        // Act
        byte[] ciphertext = Xtea.Encrypt(plaintext, key);
        byte[] decryptedPlaintext = Xtea.Decrypt(ciphertext, key);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }

    [Fact]
    public void EncryptDecrypt_EmptyPlaintext_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = [];
        new Random().NextBytes(key);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(plaintext, key));
    }
}
