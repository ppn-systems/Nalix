using Nalix.Cryptography.Symmetric.Block;
using System;
using Xunit;

namespace Nalix.Test.Cryptography.Symmetric;

public class SpeckTests
{
    private const int BLOCK_SIZE_BYTES = 8;
    private const int KEY_SIZE_BYTES = 16;

    [Fact]
    public void EncryptDecrypt_ReturnsOriginalPlaintext()
    {
        // Arrange
        byte[] plaintext = { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
        byte[] key =
        {
            0x00, 0x11, 0x22, 0x33,
            0x44, 0x55, 0x66, 0x77,
            0x88, 0x99, 0xAA, 0xBB,
            0xCC, 0xDD, 0xEE, 0xFF
        };

        // Act
        byte[] ciphertext = Speck.Encrypt(plaintext, key);
        byte[] result = Speck.Decrypt(ciphertext, key);

        // Assert
        Assert.Equal(plaintext, result);
    }

    [Fact]
    public void EncryptSpanAndDecryptSpan_ReturnOriginalPlaintext()
    {
        // Arrange
        byte[] plaintext = { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 };
        byte[] key =
        {
            0xFF, 0xEE, 0xDD, 0xCC,
            0xBB, 0xAA, 0x99, 0x88,
            0x77, 0x66, 0x55, 0x44,
            0x33, 0x22, 0x11, 0x00
        };
        byte[] ciphertext = new byte[BLOCK_SIZE_BYTES];
        byte[] decrypted = new byte[BLOCK_SIZE_BYTES];

        // Act
        Speck.Encrypt(plaintext, key, ciphertext);
        Speck.Decrypt(ciphertext, key, decrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_InvalidPlaintextLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] invalidPlaintext = new byte[BLOCK_SIZE_BYTES - 1]; // wrong size
        byte[] key = new byte[KEY_SIZE_BYTES];

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Speck.Encrypt(invalidPlaintext, key));
        Assert.Contains($"Plaintext must be exactly {BLOCK_SIZE_BYTES} bytes", ex.Message);
    }

    [Fact]
    public void Encrypt_InvalidKeyLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] plaintext = new byte[BLOCK_SIZE_BYTES];
        byte[] invalidKey = new byte[KEY_SIZE_BYTES - 1]; // wrong size

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Speck.Encrypt(plaintext, invalidKey));
        Assert.Contains($"Key must be exactly {KEY_SIZE_BYTES} bytes", ex.Message);
    }

    [Fact]
    public void Decrypt_InvalidCiphertextLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] invalidCiphertext = new byte[BLOCK_SIZE_BYTES + 1]; // wrong size
        byte[] key = new byte[KEY_SIZE_BYTES];

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Speck.Decrypt(invalidCiphertext, key));
        Assert.Contains($"Ciphertext must be exactly {BLOCK_SIZE_BYTES} bytes", ex.Message);
    }

    [Fact]
    public void Decrypt_InvalidKeyLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] ciphertext = new byte[BLOCK_SIZE_BYTES];
        byte[] invalidKey = new byte[KEY_SIZE_BYTES + 1]; // wrong size

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Speck.Decrypt(ciphertext, invalidKey));
        Assert.Contains($"Key must be exactly {KEY_SIZE_BYTES} bytes", ex.Message);
    }

    [Fact]
    public void EncryptSpan_InvalidOutputLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] plaintext = new byte[BLOCK_SIZE_BYTES];
        byte[] key = new byte[KEY_SIZE_BYTES];
        byte[] invalidOutput = new byte[BLOCK_SIZE_BYTES - 1]; // wrong size

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Speck.Encrypt(plaintext, key, invalidOutput));
        Assert.Contains($"Output must be exactly {BLOCK_SIZE_BYTES} bytes", ex.Message);
    }

    [Fact]
    public void DecryptSpan_InvalidOutputLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] ciphertext = new byte[BLOCK_SIZE_BYTES];
        byte[] key = new byte[KEY_SIZE_BYTES];
        byte[] invalidOutput = new byte[BLOCK_SIZE_BYTES + 2]; // wrong size

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Speck.Decrypt(ciphertext, key, invalidOutput));
        Assert.Contains($"Output must be exactly {BLOCK_SIZE_BYTES} bytes", ex.Message);
    }
}