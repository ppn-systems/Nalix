using Nalix.Cryptography.Symmetric;
using Nalix.Cryptography.Utilities;
using Nalix.Randomization;
using System;
using Xunit;

namespace Nalix.Cryptography.Test.Symmetric;

public class SpeckTests
{
    [Fact]
    public void Test_U8To32Little_And_ToBytes()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04];
        uint value = BitwiseUtils.U8To32Little(data, 0);
        Assert.Equal((uint)0x04030201, value); // Little-endian

        byte[] output = new byte[4];
        BitwiseUtils.ToBytes(output, value, 0);
        Assert.Equal(data, output);
    }

    [Fact]
    public void EncryptDecrypt_ValidKeyAndPlaintext_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] plaintext = new byte[8];
        RandGenerator.Fill(key);
        RandGenerator.Fill(plaintext);

        // Act
        byte[] ciphertext = Speck.Encrypt(plaintext, key);
        byte[] decrypted = Speck.Decrypt(ciphertext, key);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_InvalidKeyLength_ShouldThrowException()
    {
        // Arrange
        byte[] invalidKey = new byte[15]; // Not 16 bytes
        byte[] plaintext = new byte[8];
        RandGenerator.Fill(invalidKey);
        RandGenerator.Fill(plaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Speck.Encrypt(plaintext, invalidKey));
    }

    [Fact]
    public void EncryptDecrypt_InvalidPlaintextLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] invalidPlaintext = new byte[7]; // Not 8 bytes
        RandGenerator.Fill(key);
        RandGenerator.Fill(invalidPlaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Speck.Encrypt(invalidPlaintext, key));
    }

    [Fact]
    public void CBC_EncryptDecrypt_ValidKeyAndPlaintext_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] plaintext = new byte[8];
        RandGenerator.Fill(key);
        RandGenerator.Fill(plaintext);

        // Act
        byte[] ciphertext = Speck.CBC.Encrypt(plaintext, key);
        byte[] decryptedPlaintext = Speck.CBC.Decrypt(ciphertext, key);

        // Assert
        Assert.Equal(plaintext, decryptedPlaintext);
    }

    [Fact]
    public void CBC_Encrypt_WithoutIV_ShouldGenerateRandomIV()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] plaintext = new byte[16]; // Two blocks
        RandGenerator.Fill(key);
        RandGenerator.Fill(plaintext);

        // Act
        byte[] ciphertext1 = Speck.CBC.Encrypt(plaintext, key);
        byte[] ciphertext2 = Speck.CBC.Encrypt(plaintext, key);

        // Assert
        // The generated IVs should make the ciphertexts differ
        Assert.NotEqual(ciphertext1, ciphertext2);
    }

    [Fact]
    public void CBC_EncryptDecrypt_InvalidKeyLength_ShouldThrowException()
    {
        // Arrange
        byte[] invalidKey = new byte[15]; // Not 16 bytes
        byte[] plaintext = new byte[16]; // Two blocks
        RandGenerator.Fill(invalidKey);
        RandGenerator.Fill(plaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Speck.CBC.Encrypt(plaintext, invalidKey));
    }

    [Fact]
    public void CBC_EncryptDecrypt_InvalidPlaintextLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] invalidPlaintext = new byte[15]; // Not a multiple of 8 bytes
        RandGenerator.Fill(key);
        RandGenerator.Fill(invalidPlaintext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Speck.CBC.Encrypt(invalidPlaintext, key));
    }

    [Fact]
    public void CBC_Decrypt_InvalidCiphertextLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] invalidCiphertext = new byte[15]; // Not a multiple of 8 or less than 9 bytes
        RandGenerator.Fill(key);
        RandGenerator.Fill(invalidCiphertext);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Speck.CBC.Decrypt(invalidCiphertext, key));
    }

    [Fact]
    public void CBC_Decrypt_InvalidIVLength_ShouldThrowException()
    {
        // Arrange
        byte[] key = new byte[16];
        byte[] plaintext = new byte[16];
        RandGenerator.Fill(key);
        RandGenerator.Fill(plaintext);

        byte[] ciphertext = Speck.CBC.Encrypt(plaintext, key);
        byte[] invalidCiphertext = new byte[ciphertext.Length - 1];
        Array.Copy(ciphertext, 1, invalidCiphertext, 0, ciphertext.Length - 1); // Corrupt IV

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Speck.CBC.Decrypt(invalidCiphertext, key));
    }

    [Fact]
    public void GenerateSubkeys_ShouldReturnCorrectNumberOfSubkeys()
    {
        // Arrange
        byte[] key = new byte[16];
        RandGenerator.Fill(key);

        // Act
        var methodInfo = typeof(Speck)
            .GetMethod("GenerateSubkeys64_128", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("The method 'GenerateSubkeys64_128' was not found.");
        uint[] subkeys = (uint[])methodInfo.Invoke(null, [key])!;

        // Assert
        Assert.NotNull(subkeys);
        Assert.Equal(27, subkeys.Length); // Speck64/128 requires 27 subkeys
    }
}
