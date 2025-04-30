using Nalix.Cryptography.Symmetric;
using System;
using Xunit;

namespace Nalix.Test.Cryptography.Symmetric;

public class Arc4Tests
{
    [Fact]
    public void EncryptDecrypt_ValidKey_ShouldReturnOriginalData()
    {
        // Arrange
        byte[] key = System.Text.Encoding.UTF8.GetBytes("testkey");
        byte[] originalData = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] encryptedData = new byte[originalData.Length];
        byte[] decryptedData = new byte[originalData.Length];

        Array.Copy(originalData, encryptedData, originalData.Length);

        // Act
        using (Arc4 arc4Encryptor = new(key))
        {
            arc4Encryptor.Process(encryptedData);
        }

        using (Arc4 arc4Decryptor = new(key))
        {
            arc4Decryptor.Process(encryptedData);
        }

        Array.Copy(encryptedData, decryptedData, originalData.Length);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public void Encrypt_WithDifferentKeys_ShouldProduceDifferentCiphertext()
    {
        // Arrange
        byte[] key1 = System.Text.Encoding.UTF8.GetBytes("key1234");
        byte[] key2 = System.Text.Encoding.UTF8.GetBytes("key2345");
        byte[] originalData = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] encryptedData1 = new byte[originalData.Length];
        byte[] encryptedData2 = new byte[originalData.Length];

        Array.Copy(originalData, encryptedData1, originalData.Length);
        Array.Copy(originalData, encryptedData2, originalData.Length);

        // Act
        using (Arc4 arc4Encryptor1 = new(key1))
        {
            arc4Encryptor1.Process(encryptedData1);
        }

        using (Arc4 arc4Encryptor2 = new(key2))
        {
            arc4Encryptor2.Process(encryptedData2);
        }

        // Assert
        Assert.NotEqual(encryptedData1, encryptedData2);
    }

    [Theory]
    [InlineData(3)] // Too short
    [InlineData(300)] // Too long
    public void Constructor_InvalidKeyLength_ShouldThrowException(int keyLength)
    {
        // Arrange
        byte[] key = new byte[keyLength];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Arc4(key));
    }

    [Fact]
    public void Constructor_NullKey_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Arc4(null));
    }

    [Fact]
    public void Process_DisposedInstance_ShouldThrowObjectDisposedException()
    {
        // Arrange
        byte[] key = System.Text.Encoding.UTF8.GetBytes("testkey");
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        Arc4 arc4 = new(key);
        arc4.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => arc4.Process(data));
    }

    [Fact]
    public void Reset_ShouldClearInternalState()
    {
        // Arrange
        byte[] key = System.Text.Encoding.UTF8.GetBytes("testkey");
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] encryptedData = new byte[data.Length];

        Array.Copy(data, encryptedData, data.Length);

        using (Arc4 arc4 = new(key))
        {
            // Act
            arc4.Process(encryptedData);
            arc4.Reset();
            arc4.Process(encryptedData);
        }

        // Assert
        Assert.NotEqual(data, encryptedData);
    }

    [Fact]
    public void Dispose_ShouldClearSensitiveData()
    {
        // Arrange
        byte[] key = System.Text.Encoding.UTF8.GetBytes("testkey");
        Arc4 arc4 = new(key);

        // Act
        arc4.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => arc4.Process(new byte[1]));
    }
}
