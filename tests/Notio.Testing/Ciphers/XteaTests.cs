//using Notio.Cryptography.Ciphers.Symmetric;
//using System;
//using System.Linq;
//using Xunit;

//namespace Notio.Testing.Ciphers;

//public class XteaTests
//{
//    // Test case 1: Test invalid key length (key not exactly 4 elements)
//    [Fact]
//    public void Encrypt_InvalidKeyLength_ThrowsArgumentException()
//    {
//        // Arrange
//        var key = new uint[] { 1, 2, 3 }; // Invalid key length
//        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // Example data
//        var output = new byte[8];

//        // Act & Assert
//        var exception = Assert.Throws<ArgumentException>(() => Xtea.Encrypt(data, key, output));
//        Assert.Equal("Key must be exactly 4 elements (Parameter 'key')", exception.Message);
//    }

//    // Test case 2: Test data encryption and decryption
//    [Fact]
//    public void Encrypt_Decrypt_Correctly()
//    {
//        // Arrange
//        var key = new uint[] { 1, 2, 3, 4 }; // Valid key
//        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // Example data
//        var encryptedData = new byte[8];
//        var decryptedData = new byte[8];

//        // Act: Encrypt the data
//        Xtea.Encrypt(data, key, encryptedData);

//        // Assert: Encrypted data should not match original data
//        Assert.False(data.SequenceEqual(encryptedData), "Encrypted data should be different from the original data.");

//        // Act: Decrypt the data
//        Xtea.Decrypt(encryptedData, key, decryptedData);

//        // Assert: Decrypted data should match the original data
//        Assert.True(data.SequenceEqual(decryptedData), "Decrypted data should match the original data.");
//    }

//    // Test case 3: Test TryDecrypt method with valid data
//    [Fact]
//    public void TryDecrypt_ValidData_ReturnsTrue()
//    {
//        // Arrange
//        var key = new uint[] { 1, 2, 3, 4 }; // Valid key
//        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // Example data
//        var encryptedData = new byte[8];
//        var decryptedData = new byte[8];

//        // Act: Encrypt the data
//        Xtea.Encrypt(data, key, encryptedData);

//        // Act: Try to decrypt the encrypted data
//        var result = Xtea.TryDecrypt(encryptedData, key, decryptedData);

//        // Assert: Decryption should succeed
//        Assert.True(result, "Decryption should succeed.");
//        Assert.True(data.SequenceEqual(decryptedData), "Decrypted data should match the original data.");
//    }

//    // Test case 4: Test TryDecrypt method with invalid data (wrong key)
//    [Fact]
//    public void TryDecrypt_InvalidData_ReturnsFalse()
//    {
//        // Arrange
//        var key = new uint[] { 1, 2, 3, 4 }; // Correct key
//        var wrongKey = new uint[] { 5, 6, 7, 8 }; // Wrong key
//        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // Example data
//        var encryptedData = new byte[8];
//        var decryptedData = new byte[8];

//        // Act: Encrypt the data with the correct key
//        Xtea.Encrypt(data, key, encryptedData);

//        // Act: Try to decrypt the data with the wrong key
//        var result = Xtea.TryDecrypt(encryptedData, wrongKey, decryptedData);

//        // Assert: Decryption should fail
//        Assert.False(result, "Decryption should fail with the wrong key.");
//    }

//    // Test case 5: Test empty data input (Encrypt)
//    [Fact]
//    public void Encrypt_EmptyData_ThrowsArgumentException()
//    {
//        // Arrange
//        var key = new uint[] { 1, 2, 3, 4 }; // Valid key
//        var data = new byte[0]; // Empty data
//        var output = new byte[0];

//        // Act & Assert
//        var exception = Assert.Throws<ArgumentException>(() => Xtea.Encrypt(data, key, output));
//        Assert.Equal("Data cannot be empty (Parameter 'data')", exception.Message);
//    }

//    // Test case 6: Test invalid data length for Decrypt (non-multiple of 8)
//    [Fact]
//    public void Decrypt_InvalidDataLength_ThrowsArgumentException()
//    {
//        // Arrange
//        var key = new uint[] { 1, 2, 3, 4 }; // Valid key
//        var data = new byte[] { 1, 2, 3, 4, 5 }; // Invalid data length (not multiple of 8)
//        var output = new byte[8];

//        // Act & Assert
//        var exception = Assert.Throws<ArgumentException>(() => Xtea.Decrypt(data, key, output));
//        Assert.Equal("Invalid input data or key. (Parameter 'data')", exception.Message);
//    }
//}
