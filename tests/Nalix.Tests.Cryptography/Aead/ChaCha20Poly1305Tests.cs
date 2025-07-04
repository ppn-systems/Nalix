using Nalix.Cryptography.Aead;
using System;
using Xunit;

namespace Nalix.Test.Cryptography.Aead;

public class ChaCha20Poly1305Tests
{
    [Fact]
    public void Encrypt_ValidInput_ShouldEncryptSuccessfully()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] aad = System.Text.Encoding.UTF8.GetBytes("Associated Data");

        Random random = new();
        random.NextBytes(key);
        random.NextBytes(nonce);

        // Act
        byte[] encryptedData = ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad);

        // Assert
        Assert.NotNull(encryptedData);
        Assert.True(encryptedData.Length > plaintext.Length); // Should include tag
    }

    [Fact]
    public void Decrypt_ValidInput_ShouldDecryptSuccessfully()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] aad = System.Text.Encoding.UTF8.GetBytes("Associated Data");

        Random random = new();
        random.NextBytes(key);
        random.NextBytes(nonce);

        byte[] encryptedData = ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad);

        // Act
        byte[] decryptedData = ChaCha20Poly1305.Decrypt(key, nonce, encryptedData, aad);

        // Assert
        Assert.NotNull(decryptedData);
        Assert.Equal(plaintext, decryptedData);
    }

    [Fact]
    public void Decrypt_InvalidTag_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        byte[] aad = System.Text.Encoding.UTF8.GetBytes("Associated Data");

        Random random = new();
        random.NextBytes(key);
        random.NextBytes(nonce);

        byte[] encryptedData = ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad);

        // Tamper with the tag
        encryptedData[^1] ^= 0xFF;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            ChaCha20Poly1305.Decrypt(key, nonce, encryptedData, aad);
        });
    }

    [Fact]
    public void EncryptDecrypt_EmptyPlaintext_ShouldWorkCorrectly()
    {
        // Arrange
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = [];
        byte[] aad = System.Text.Encoding.UTF8.GetBytes("Associated Data");

        Random random = new();
        random.NextBytes(key);
        random.NextBytes(nonce);

        // Act
        byte[] encryptedData = ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad);
        byte[] decryptedData = ChaCha20Poly1305.Decrypt(key, nonce, encryptedData, aad);

        // Assert
        Assert.NotNull(decryptedData);
        Assert.Empty(decryptedData);
    }
}