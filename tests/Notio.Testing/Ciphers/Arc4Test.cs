using Notio.Cryptography.Ciphers.Symmetric;
using System;
using System.Linq;
using Xunit;

namespace Notio.Testing.Ciphers;

public class Arc4Tests
{
    // Test case 1: Test invalid key (key length less than 5 bytes)
    [Fact]
    public void Constructor_InvalidKey_ThrowsArgumentException()
    {
        // Arrange
        byte[] invalidKey = new byte[] { 1, 2, 3, 4 }; // Key length less than 5 bytes

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new Arc4(invalidKey));
        Assert.Equal("Key length must be between 5 and 256 bytes. (Parameter 'key')", exception.Message);
    }

    // Test case 2: Test valid key and initialization
    [Fact]
    public void Constructor_ValidKey_InitializesCorrectly()
    {
        // Arrange
        byte[] key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // Valid key of length 10

        // Act
        var arc4 = new Arc4(key);

        // Assert
        Assert.NotNull(arc4);
    }

    // Test case 3: Test Process method encryption and decryption
    [Fact]
    public void Process_EncryptsAndDecryptsDataCorrectly()
    {
        // Arrange
        byte[] key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // Valid key
        var arc4 = new Arc4(key);
        byte[] data = new byte[] { 10, 20, 30, 40, 50, 60 }; // Example data to encrypt

        // Act: Encrypt the data
        var encryptedData = new byte[data.Length];
        data.CopyTo(encryptedData, 0); // Copy original data to encryptedData
        arc4.Process(encryptedData);

        // Assert: Encrypted data should be different from the original data
        Assert.False(data.SequenceEqual(encryptedData), "Encrypted data should be different from the original data.");

        // Act: Decrypt the encrypted data
        arc4.Process(encryptedData);

        // Assert: After decryption, the data should match the original data
        Assert.True(data.SequenceEqual(encryptedData), "Decrypted data should match the original data.");
    }

    // Test case 4: Test multiple consecutive encryption/decryption operations
    [Fact]
    public void Process_MultipleOperations_Success()
    {
        // Arrange
        byte[] key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // Valid key
        var arc4 = new Arc4(key);
        byte[] data = new byte[] { 100, 200, 50, 255, 0 }; // Example data to encrypt

        // Act: Encrypt the data
        var encryptedData = new byte[data.Length];
        data.CopyTo(encryptedData, 0); // Copy original data to encryptedData
        arc4.Process(encryptedData);

        // Assert: Encrypted data should be different from the original data
        Assert.False(data.SequenceEqual(encryptedData), "Encrypted data should be different from the original data.");

        // Act: Encrypt the data again (RC4 works symmetrically, so double encryption should give the original data)
        arc4.Process(encryptedData);

        // Assert: After second operation, the data should match the original data
        Assert.True(data.SequenceEqual(encryptedData), "Data should match the original data after double encryption.");
    }
}
