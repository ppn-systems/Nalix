using Nalix.Cryptography.Symmetric;
using System;
using Xunit;

namespace Nalix.Test.Cryptography.Symmetric;

public class TwofishTests
{
    private const int BlockSize = 16;

    #region ECB Mode Tests

    [Fact]
    public void ECB_EncryptAndDecrypt_ReturnsOriginalPlaintext()
    {
        // Arrange
        // 16 bytes key (128-bit)
        byte[] key = new byte[16]
        {
            0x00, 0x11, 0x22, 0x33,
            0x44, 0x55, 0x66, 0x77,
            0x88, 0x99, 0xAA, 0xBB,
            0xCC, 0xDD, 0xEE, 0xFF
        };

        // 16 bytes plaintext (one block)
        byte[] plaintext = new byte[BlockSize]
        {
            0x10, 0x20, 0x30, 0x40,
            0x50, 0x60, 0x70, 0x80,
            0x90, 0xA0, 0xB0, 0xC0,
            0xD0, 0xE0, 0xF0, 0x00
        };

        // Act
        byte[] ciphertext = Twofish.ECB.Encrypt(key, plaintext);
        byte[] decrypted = Twofish.ECB.Decrypt(key, ciphertext);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void ECB_Encrypt_WithInvalidPlaintextLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        // Plaintext length not multiple of 16 bytes
        byte[] invalidPlaintext = new byte[10];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Encrypt(key, invalidPlaintext));
    }

    [Fact]
    public void ECB_Decrypt_WithInvalidCiphertextLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        // Ciphertext length not multiple of 16 bytes
        byte[] invalidCiphertext = new byte[10];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Decrypt(key, invalidCiphertext));
    }

    [Fact]
    public void ECB_Encrypt_WithOutputBufferSmallerThanPlaintext_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] plaintext = new byte[BlockSize * 2]; // Two blocks
        byte[] smallOutput = new byte[BlockSize];    // Too small buffer

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Encrypt(key, plaintext, smallOutput));
    }

    [Fact]
    public void ECB_Decrypt_WithOutputBufferSmallerThanCiphertext_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] ciphertext = new byte[BlockSize * 2]; // Two blocks
        byte[] smallOutput = new byte[BlockSize];      // Too small buffer

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Decrypt(key, ciphertext, smallOutput));
    }

    #endregion ECB Mode Tests

    #region CBC Mode Tests

    [Fact]
    public void CBC_EncryptAndDecrypt_ReturnsOriginalPlaintext()
    {
        // Arrange
        // 16 bytes key (128-bit)
        byte[] key = new byte[16]
        {
            0xFF, 0xEE, 0xDD, 0xCC,
            0xBB, 0xAA, 0x99, 0x88,
            0x77, 0x66, 0x55, 0x44,
            0x33, 0x22, 0x11, 0x00
        };

        // 16 bytes IV (block size)
        byte[] iv = new byte[BlockSize]
        {
            0x0F, 0x1E, 0x2D, 0x3C,
            0x4B, 0x5A, 0x69, 0x78,
            0x87, 0x96, 0xA5, 0xB4,
            0xC3, 0xD2, 0xE1, 0xF0
        };

        // 32 bytes plaintext (two blocks)
        byte[] plaintext = new byte[BlockSize * 2]
        {
            0x11, 0x22, 0x33, 0x44,
            0x55, 0x66, 0x77, 0x88,
            0x99, 0xAA, 0xBB, 0xCC,
            0xDD, 0xEE, 0xFF, 0x00,
            0x10, 0x20, 0x30, 0x40,
            0x50, 0x60, 0x70, 0x80,
            0x90, 0xA0, 0xB0, 0xC0,
            0xD0, 0xE0, 0xF0, 0x00
        };

        // Act
        byte[] ciphertext = Twofish.CBC.Encrypt(key, iv, plaintext);
        byte[] decrypted = Twofish.CBC.Decrypt(key, iv, ciphertext);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void CBC_Encrypt_WithInvalidIVLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] iv = new byte[10]; // Invalid IV length
        byte[] plaintext = new byte[BlockSize];

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Twofish.CBC.Encrypt(key, iv, plaintext));
        Assert.Contains($"IV must be {BlockSize} bytes", ex.Message);
    }

    [Fact]
    public void CBC_Decrypt_WithInvalidIVLength_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] iv = new byte[10]; // Invalid IV length
        byte[] ciphertext = new byte[BlockSize];

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Twofish.CBC.Decrypt(key, iv, ciphertext));
        Assert.Contains($"IV must be {BlockSize} bytes", ex.Message);
    }

    [Fact]
    public void CBC_Encrypt_WithOutputBufferSmallerThanPlaintext_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] iv = new byte[BlockSize];
        byte[] plaintext = new byte[BlockSize * 2]; // Two blocks
        byte[] smallOutput = new byte[BlockSize];     // Too small buffer

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.CBC.Encrypt(key, iv, plaintext, smallOutput));
    }

    [Fact]
    public void CBC_Decrypt_WithOutputBufferSmallerThanCiphertext_ThrowsArgumentException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] iv = new byte[BlockSize];
        byte[] ciphertext = new byte[BlockSize * 2]; // Two blocks
        byte[] smallOutput = new byte[BlockSize];       // Too small buffer

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.CBC.Decrypt(key, iv, ciphertext, smallOutput));
    }

    #endregion CBC Mode Tests

    #region Key and Data Validation Tests

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(20)]
    public void ECB_Encrypt_WithInvalidKeyLength_ThrowsArgumentException(int keyLength)
    {
        // Arrange
        byte[] key = new byte[keyLength]; // Invalid key length (must be 16, 24, or 32)
        byte[] plaintext = new byte[BlockSize];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Encrypt(key, plaintext));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(20)]
    public void CBC_Encrypt_WithInvalidKeyLength_ThrowsArgumentException(int keyLength)
    {
        // Arrange
        byte[] key = new byte[keyLength]; // Invalid key length
        byte[] iv = new byte[BlockSize];
        byte[] plaintext = new byte[BlockSize];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.CBC.Encrypt(key, iv, plaintext));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ECB_Encrypt_WithDataNotMultipleOfBlockSize_ThrowsArgumentException(int dataLength)
    {
        // Arrange
        byte[] key = new byte[16];
        // Data length not a multiple of the block size (16 bytes)
        byte[] plaintext = new byte[dataLength];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Twofish.ECB.Encrypt(key, plaintext));
    }

    #endregion Key and Data Validation Tests
}
