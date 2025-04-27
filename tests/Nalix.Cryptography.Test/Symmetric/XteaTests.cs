using Nalix.Cryptography.Symmetric;
using Nalix.Randomization;
using System;
using Xunit;

namespace Nalix.Cryptography.Test.Symmetric;

public class XteaTests
{
    [Fact]
    public void Encrypt_DecryptWithSameKey_ReturnsOriginalData()
    {
        // Arrange
        byte[] data = new byte[] { 1, 2, 3, 4, 5 };
        uint[] key = new uint[] { 1, 2, 3, 4 };

        // Act
        byte[] encrypted = Xtea.Encrypt(data, key);
        byte[] decrypted = Xtea.Decrypt(encrypted, key);

        // Assert
        Assert.Equal(data, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_ValidKeyAndPlaintext_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, XTEA!");
        RandGenerator.Fill(key);

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
        RandGenerator.Fill(key);
        RandGenerator.Fill(iv);

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
        RandGenerator.Fill(key);

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
        RandGenerator.Fill(invalidKey);

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
        RandGenerator.Fill(key);
        RandGenerator.Fill(invalidIV);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(plaintext, key, invalidIV));
    }

    [Fact]
    public void Decrypt_InvalidPadding_ShouldThrowCryptoException()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Invalid padding test");
        RandGenerator.Fill(key);

        byte[] ciphertext = Xtea.Encrypt(plaintext, key);

        // Corrupt the padding bytes in the ciphertext
        ciphertext[^1] = 0xFF;

        // Act & Assert
        Assert.Throws<Common.Exceptions.CryptoException>(() => Xtea.Decrypt(ciphertext, key));
    }

    [Fact]
    public void EncryptDecrypt_LargeData_ShouldWorkCorrectly()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = new byte[1024]; // 1 MB of data
        RandGenerator.Fill(key);
        RandGenerator.Fill(plaintext);

        // Assert
        Assert.Equal(plaintext, Xtea.Decrypt(Xtea.Encrypt(plaintext, key), key));
    }

    [Fact]
    public void EncryptDecrypt_EmptyPlaintext_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[Xtea.KeySizeInBytes];
        byte[] plaintext = [];
        RandGenerator.Fill(key);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(plaintext, key));
    }
}
