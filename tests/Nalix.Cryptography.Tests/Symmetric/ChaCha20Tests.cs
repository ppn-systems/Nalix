using Nalix.Common.Enums;
using Nalix.Cryptography.Symmetric.Stream;
using System;
using System.Text;
using Xunit;

namespace Nalix.Cryptography.Tests.Symmetric;

public class ChaCha20Tests
{
    [Fact]
    public void EncryptDecrypt_InvalidKey_ShouldThrowException()
    {
        // Arrange
        Byte[] invalidKey = new Byte[ChaCha20.KeySize - 1];
        Byte[] nonce = new Byte[ChaCha20.NonceSize];
        new Random().NextBytes(invalidKey);
        new Random().NextBytes(nonce);

        // Act & Assert
        _ = Assert.Throws<ArgumentException>(() => new ChaCha20(invalidKey, nonce, 0));
    }

    [Fact]
    public void EncryptDecrypt_InvalidNonce_ShouldThrowException()
    {
        // Arrange
        Byte[] key = new Byte[ChaCha20.KeySize];
        Byte[] invalidNonce = new Byte[ChaCha20.NonceSize - 1];
        new Random().NextBytes(key);
        new Random().NextBytes(invalidNonce);

        // Act & Assert
        _ = Assert.Throws<ArgumentException>(() => new ChaCha20(key, invalidNonce, 0));
    }

    [Fact]
    public void EncryptDecrypt_DisposedInstance_ShouldThrowException()
    {
        // Arrange
        Byte[] key = new Byte[ChaCha20.KeySize];
        Byte[] nonce = new Byte[ChaCha20.NonceSize];
        Byte[] data = Encoding.UTF8.GetBytes("Hello, ChaCha20!");
        Byte[] output = new Byte[data.Length];
        new Random().NextBytes(key);
        new Random().NextBytes(nonce);

        var chaCha20 = new ChaCha20(key, nonce, 0);
        chaCha20.Dispose();

        // Act & Assert
        _ = Assert.Throws<ObjectDisposedException>(() => chaCha20.EncryptBytes(output, data, data.Length));
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