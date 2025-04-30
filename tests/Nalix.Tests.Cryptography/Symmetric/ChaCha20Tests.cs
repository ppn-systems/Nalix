using Nalix.Cryptography.Enums;
using Nalix.Cryptography.Symmetric;
using System;
using System.Text;
using Xunit;

namespace Nalix.Test.Cryptography.Symmetric;

public class ChaCha20Tests
{
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
