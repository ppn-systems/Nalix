// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System.Security.Cryptography;
using Nalix.Shared.Security.Aead;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

/// <summary>
/// Unit tests for Salsa20Poly1305 AEAD implementation.
/// </summary>
public class Salsa20Poly1305Tests
{
    private static byte[] RandomBytes(int length)
    {
        byte[] buf = new byte[length];
        RandomNumberGenerator.Fill(buf);
        return buf;
    }

    /// <summary>
    /// Verifies round-trip correctness using the Span-based API for both 16- and 32-byte keys.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void EncryptDecryptRoundTripSpan(int keySize)
    {
        // Arrange
        byte[] key = RandomBytes(keySize);
        byte[] nonce = RandomBytes(8); // Salsa20 nonce size (8)
        byte[] plaintext = RandomBytes(128);
        byte[] aad = RandomBytes(20);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];
        byte[] recovered = new byte[plaintext.Length];

        // Act: encrypt (Span API)
        int ctWritten = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);
        Assert.Equal(plaintext.Length, ctWritten);

        // Act: decrypt (Span API)
        int ptWritten = Salsa20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);
        Assert.Equal(plaintext.Length, ptWritten);

        // Assert: plaintext matches
        Assert.Equal(plaintext, recovered);
    }

    /// <summary>
    /// Tampering with the authentication tag should cause Span-based decryption to return a negative value.
    /// </summary>
    [Fact]
    public void DecryptTamperedTagReturnsNegativeSpan()
    {
        // Arrange
        byte[] key = RandomBytes(32);
        byte[] nonce = RandomBytes(8);
        byte[] plaintext = RandomBytes(32);
        byte[] aad = RandomBytes(4);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];

        _ = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        // Tamper with tag
        tag[0] ^= 0xFF;

        // Destination buffer for plaintext
        byte[] recovered = new byte[plaintext.Length];

        // Act
        int result = Salsa20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);

        // Assert: decryption should fail (method returns negative according to implementation)
        Assert.True(result < 0, "Span-based Decrypt should return a negative value on authentication failure.");
    }
}