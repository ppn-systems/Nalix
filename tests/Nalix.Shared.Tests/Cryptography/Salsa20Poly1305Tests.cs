using Nalix.Common.Exceptions;
using Nalix.Shared.Security.Aead;
using System;
using System.Security.Cryptography;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

/// <summary>
/// Unit tests for Salsa20Poly1305 AEAD implementation.
/// </summary>
public class Salsa20Poly1305Tests
{
    private static Byte[] RandomBytes(Int32 length)
    {
        var buf = new Byte[length];
        RandomNumberGenerator.Fill(buf);
        return buf;
    }

    /// <summary>
    /// Verifies round-trip correctness using the Span-based API for both 16- and 32-byte keys.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void EncryptDecrypt_RoundTrip_Span(Int32 keySize)
    {
        // Arrange
        Byte[] key = RandomBytes(keySize);
        Byte[] nonce = RandomBytes(8); // Salsa20 nonce size (8)
        Byte[] plaintext = RandomBytes(128);
        Byte[] aad = RandomBytes(20);

        var ciphertext = new Byte[plaintext.Length];
        var tag = new Byte[Salsa20Poly1305.TagSize];
        var recovered = new Byte[plaintext.Length];

        // Act: encrypt (Span API)
        Int32 ctWritten = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);
        Assert.Equal(plaintext.Length, ctWritten);

        // Act: decrypt (Span API)
        Int32 ptWritten = Salsa20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);
        Assert.Equal(plaintext.Length, ptWritten);

        // Assert: plaintext matches
        Assert.Equal(plaintext, recovered);
    }

    /// <summary>
    /// Verifies round-trip correctness using the array convenience API for both 16- and 32-byte keys.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void EncryptDecrypt_RoundTrip_ArrayOverloads(Int32 keySize)
    {
        // Arrange
        Byte[] key = RandomBytes(keySize);
        Byte[] nonce = RandomBytes(8);
        Byte[] plaintext = RandomBytes(64);
        Byte[] aad = RandomBytes(8);

        // Act: encrypt (array API)
        Byte[] cipherWithTag = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad);

        // Act: decrypt (array API)
        Byte[] decrypted = Salsa20Poly1305.Decrypt(key, nonce, cipherWithTag, aad);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    /// <summary>
    /// Tampering with the authentication tag should cause array API decryption to throw a CryptoException.
    /// </summary>
    [Fact]
    public void Decrypt_TamperedTag_ThrowsCryptoException_Array()
    {
        // Arrange
        Byte[] key = RandomBytes(32);
        Byte[] nonce = RandomBytes(8);
        Byte[] plaintext = RandomBytes(48);
        Byte[] aad = RandomBytes(12);

        Byte[] cipherWithTag = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad);

        // Tamper with the tag (flip one bit in last byte)
        cipherWithTag[^1] ^= 0x01;

        // Act & Assert: array Decrypt() should throw the library's CryptoException
        Assert.Throws<CryptoException>(() => Salsa20Poly1305.Decrypt(key, nonce, cipherWithTag, aad));
    }

    /// <summary>
    /// Tampering with the authentication tag should cause Span-based decryption to return a negative value.
    /// </summary>
    [Fact]
    public void Decrypt_TamperedTag_ReturnsNegative_Span()
    {
        // Arrange
        Byte[] key = RandomBytes(32);
        Byte[] nonce = RandomBytes(8);
        Byte[] plaintext = RandomBytes(32);
        Byte[] aad = RandomBytes(4);

        var ciphertext = new Byte[plaintext.Length];
        var tag = new Byte[Salsa20Poly1305.TagSize];

        Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        // Tamper with tag
        tag[0] ^= 0xFF;

        // Destination buffer for plaintext
        var recovered = new Byte[plaintext.Length];

        // Act
        Int32 result = Salsa20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);

        // Assert: decryption should fail (method returns negative according to implementation)
        Assert.True(result < 0, "Span-based Decrypt should return a negative value on authentication failure.");
    }

    /// <summary>
    /// Passing invalid key/nonce lengths should raise an ArgumentException for array overloads (per API docs).
    /// </summary>
    [Fact]
    public void InvalidKeyOrNonceLength_ThrowsArgumentException_ArrayOverloads()
    {
        Byte[] shortKey = new Byte[10];
        Byte[] validNonce = RandomBytes(8);
        Byte[] plaintext = RandomBytes(16);

        Assert.Throws<CryptoException>(() => Salsa20Poly1305.Encrypt(shortKey, validNonce, plaintext));

        Byte[] validKey = RandomBytes(32);
        Byte[] shortNonce = new Byte[5];

        Assert.Throws<CryptoException>(() => Salsa20Poly1305.Encrypt(validKey, shortNonce, plaintext));
    }

    /// <summary>
    /// Empty plaintext and/or empty AAD should be handled correctly.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 16)]
    [InlineData(32, 0)]
    public void EmptyPlaintextOrAad_Works(Int32 plaintextLen, Int32 aadLen)
    {
        Byte[] key = RandomBytes(32);
        Byte[] nonce = RandomBytes(8);
        Byte[] plaintext = RandomBytes(plaintextLen);
        Byte[] aad = RandomBytes(aadLen);

        Byte[] cipherWithTag = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad);
        Byte[] decrypted = Salsa20Poly1305.Decrypt(key, nonce, cipherWithTag, aad);

        Assert.Equal(plaintext, decrypted);
    }
}