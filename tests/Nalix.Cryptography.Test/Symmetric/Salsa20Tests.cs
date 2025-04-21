using Notio.Cryptography.Symmetric;
using System;
using System.Text;
using Xunit;

namespace Notio.Cryptography.Test.Symmetric;
public class Salsa20Tests
{
    [Fact]
    public void EncryptDecrypt_ValidKeyNonce_ShouldReturnOriginalData()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[8];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        byte[] originalData = Encoding.UTF8.GetBytes("Hello, Salsa20!");
        ulong counter = 0;

        // Act
        byte[] encryptedData = Salsa20.Encrypt(key, nonce, counter, originalData);
        byte[] decryptedData = Salsa20.Decrypt(key, nonce, counter, encryptedData);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public void EncryptDecrypt_InvalidKeyLength_ShouldThrowException()
    {
        // Arrange
        byte[] invalidKey = new byte[31]; // Key length is not 32 bytes
        byte[] nonce = new byte[8];
        new Random().NextBytes(invalidKey);
        new Random().NextBytes(nonce);

        byte[] data = Encoding.UTF8.GetBytes("Invalid key test");
        ulong counter = 0;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Salsa20.Encrypt(invalidKey, nonce, counter, data));
    }

    [Fact]
    public void EncryptDecrypt_InvalidNonceLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] invalidNonce = new byte[7]; // Nonce length is not 8 bytes
        new Random().NextBytes(key);
        new Random().NextBytes(invalidNonce);

        byte[] data = Encoding.UTF8.GetBytes("Invalid nonce test");
        ulong counter = 0;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Salsa20.Encrypt(key, invalidNonce, counter, data));
    }

    [Fact]
    public void EncryptDecrypt_EmptyInput_ShouldReturnEmptyOutput()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[8];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        byte[] originalData = [];
        ulong counter = 0;

        // Act
        byte[] encryptedData = Salsa20.Encrypt(key, nonce, counter, originalData);
        byte[] decryptedData = Salsa20.Decrypt(key, nonce, counter, encryptedData);

        // Assert
        Assert.Empty(encryptedData);
        Assert.Empty(decryptedData);
    }

    [Fact]
    public void EncryptDecrypt_BufferTooSmall_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[8];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        byte[] data = Encoding.UTF8.GetBytes("Buffer too small test");
        byte[] outputBuffer = new byte[data.Length - 1]; // Smaller than input
        ulong counter = 0;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Salsa20.Encrypt(key, nonce, counter, data, outputBuffer));
    }

    [Fact]
    public void DeriveKeyFromPassphrase_ShouldReturn32ByteKey()
    {
        // Arrange
        string passphrase = "secure-passphrase";

        // Act
        byte[] key = Salsa20.DeriveKeyFromPassphrase(passphrase);

        // Assert
        Assert.NotNull(key);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void DeriveKeyFromPassphrase_EmptyPassphrase_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Salsa20.DeriveKeyFromPassphrase(string.Empty));
    }

    [Fact]
    public void EncryptDecrypt_LargeData_ShouldWorkCorrectly()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[8];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        byte[] originalData = new byte[1024 * 1024]; // 1 MB of data
        new Random().NextBytes(originalData);
        byte[] encryptedData = new byte[originalData.Length];
        byte[] decryptedData = new byte[originalData.Length];
        ulong counter = 0;

        // Act
        Salsa20.Encrypt(key, nonce, counter, originalData, encryptedData);
        Salsa20.Decrypt(key, nonce, counter, encryptedData, decryptedData);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }
}
