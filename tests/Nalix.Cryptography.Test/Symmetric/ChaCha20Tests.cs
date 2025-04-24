using Nalix.Cryptography.Enums;
using Nalix.Cryptography.Symmetric;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Nalix.Cryptography.Test.Symmetric;

public class ChaCha20Tests
{
    [Fact]
    public void EncryptDecrypt_ValidKeyNonce_ShouldReturnOriginalData()
    {
        // Arrange
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] nonce = new byte[ChaCha20.NonceSize];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        byte[] originalData = Encoding.UTF8.GetBytes("Hello, ChaCha20!");
        byte[] encryptedData = new byte[originalData.Length];
        byte[] decryptedData = new byte[originalData.Length];

        using var chaCha20 = new ChaCha20(key, nonce, 0);

        // Act
        chaCha20.EncryptBytes(encryptedData, originalData, originalData.Length);
        chaCha20.DecryptBytes(decryptedData, encryptedData, encryptedData.Length);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public void EncryptDecrypt_Stream_ShouldReturnOriginalData()
    {
        // Arrange
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] nonce = new byte[ChaCha20.NonceSize];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        byte[] originalData = Encoding.UTF8.GetBytes("Hello, ChaCha20!");
        using MemoryStream inputStream = new(originalData);
        using MemoryStream encryptedStream = new();
        using MemoryStream decryptedStream = new();

        using var chaCha20 = new ChaCha20(key, nonce, 0);

        // Act
        chaCha20.EncryptStream(encryptedStream, inputStream);
        encryptedStream.Seek(0, SeekOrigin.Begin);
        chaCha20.DecryptStream(decryptedStream, encryptedStream);

        // Assert
        Assert.Equal(originalData, decryptedStream.ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task EncryptDecrypt_StreamAsync_ShouldReturnOriginalData()
    {
        // Arrange
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] nonce = new byte[ChaCha20.NonceSize];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        byte[] originalData = Encoding.UTF8.GetBytes("Hello, ChaCha20!");
        using MemoryStream inputStream = new(originalData);
        using MemoryStream encryptedStream = new();
        using MemoryStream decryptedStream = new();

        using var chaCha20 = new ChaCha20(key, nonce, 0);

        // Act
        await chaCha20.EncryptStreamAsync(encryptedStream, inputStream);
        encryptedStream.Seek(0, SeekOrigin.Begin);
        await chaCha20.DecryptStreamAsync(decryptedStream, encryptedStream);

        // Assert
        Assert.Equal(originalData, decryptedStream.ToArray());
    }

    [Fact]
    public void EncryptDecrypt_InvalidKey_ShouldThrowException()
    {
        // Arrange
        byte[] invalidKey = new byte[ChaCha20.KeySize - 1];
        byte[] nonce = new byte[ChaCha20.NonceSize];
        new Random().NextBytes(invalidKey);
        new Random().NextBytes(nonce);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ChaCha20(invalidKey, nonce, 0));
    }

    [Fact]
    public void EncryptDecrypt_InvalidNonce_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] invalidNonce = new byte[ChaCha20.NonceSize - 1];
        new Random().NextBytes(key);
        new Random().NextBytes(invalidNonce);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ChaCha20(key, invalidNonce, 0));
    }

    [Fact]
    public void EncryptDecrypt_DisposedInstance_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] nonce = new byte[ChaCha20.NonceSize];
        byte[] data = Encoding.UTF8.GetBytes("Hello, ChaCha20!");
        byte[] output = new byte[data.Length];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        var chaCha20 = new ChaCha20(key, nonce, 0);
        chaCha20.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => chaCha20.EncryptBytes(output, data, data.Length));
    }

    [Fact]
    public void EncryptDecrypt_UTF8String_ShouldReturnOriginalString()
    {
        // Arrange
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] nonce = new byte[ChaCha20.NonceSize];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        string originalString = "Hello, ChaCha20!";
        using var chaCha20 = new ChaCha20(key, nonce, 0);

        // Act
        byte[] encryptedBytes = chaCha20.EncryptString(originalString);
        string decryptedString = chaCha20.DecryptUtf8ByteArray(encryptedBytes);

        // Assert
        Assert.Equal(originalString, decryptedString);
    }

    [Fact]
    public void DetectSimdMode_ShouldReturnCorrectSimdMode()
    {
        // Arrange & Act
        var detectSimdModeMethod = typeof(ChaCha20)
            .GetMethod("DetectSimdMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) ?? throw new InvalidOperationException("The method 'DetectSimdMode' could not be found.");
        SimdMode simdMode = (SimdMode)detectSimdModeMethod.Invoke(null, null)!;

        // Assert
        Assert.True(Enum.IsDefined(simdMode));
    }
}
