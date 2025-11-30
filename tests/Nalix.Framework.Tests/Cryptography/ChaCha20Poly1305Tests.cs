// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Framework.Security.Aead;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// Unit tests for ChaCha20Poly1305 AEAD implementation.
/// </summary>
public class ChaCha20Poly1305Tests
{
    private static byte[] RandomBytes(int length)
    {
        byte[] buf = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buf);
        return buf;
    }

    /// <summary>
    /// Verifies round-trip correctness using the Span-based API.
    /// </summary>
    [Fact]
    public void EncryptDecryptRoundTripSpan()
    {
        // Arrange
        byte[] key = RandomBytes(32);
        byte[] nonce = RandomBytes(12);
        byte[] plaintext = RandomBytes(128);
        byte[] aad = RandomBytes(20);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[ChaCha20Poly1305.TagSize];
        byte[] recovered = new byte[plaintext.Length];

        // Act: encrypt (Span API)
        int ctWritten = ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);
        Assert.Equal(plaintext.Length, ctWritten);

        // Act: decrypt (Span API)
        int ptWritten = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);
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
        byte[] nonce = RandomBytes(12);
        byte[] plaintext = RandomBytes(32);
        byte[] aad = RandomBytes(4);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[ChaCha20Poly1305.TagSize];

        _ = ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        // Tamper with tag
        tag[0] ^= 0xFF;

        // Destination buffer for plaintext
        byte[] recovered = new byte[plaintext.Length];

        // Act
        int result = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);

        // Assert: decryption should fail (method returns negative according to implementation)
        Assert.True(result < 0, "Span-based Decrypt should return a negative value on authentication failure.");
    }
}
