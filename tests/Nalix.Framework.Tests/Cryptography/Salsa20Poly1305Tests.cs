// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Security.Cryptography;
using Nalix.Codec.Security.Aead;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// Unit tests for Salsa20Poly1305 AEAD implementation.
/// </summary>
public sealed class Salsa20Poly1305Tests
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
    public void EncryptThenDecryptWithSpanApiRoundTripsPayload(int keySize)
    {
        byte[] key = RandomBytes(keySize);
        byte[] nonce = RandomBytes(8);
        byte[] plaintext = RandomBytes(128);
        byte[] aad = RandomBytes(20);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];
        byte[] recovered = new byte[plaintext.Length];

        int ctWritten = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);
        Assert.Equal(plaintext.Length, ctWritten);

        int ptWritten = Salsa20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);
        Assert.Equal(plaintext.Length, ptWritten);

        Assert.Equal(plaintext, recovered);
    }

    /// <summary>
    /// Tampering with the authentication tag should cause Span-based decryption to return a negative value.
    /// </summary>
    [Fact]
    public void DecryptWhenTagIsTamperedReturnsNegativeResult()
    {
        byte[] key = RandomBytes(32);
        byte[] nonce = RandomBytes(8);
        byte[] plaintext = RandomBytes(32);
        byte[] aad = RandomBytes(4);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];

        _ = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);
        tag[0] ^= 0xFF;
        byte[] recovered = new byte[plaintext.Length];

        int result = Salsa20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);

        Assert.True(result < 0, "Span-based Decrypt should return a negative value on authentication failure.");
    }

    /// <summary>
    /// Regression known-answer test. Salsa20Poly1305 is a custom Nalix construction with no
    /// published external test vectors (it is NOT compatible with NaCl's crypto_secretbox, which
    /// uses XSalsa20 with a 24-byte nonce and a different MAC key derivation). This test pins the
    /// current implementation's byte-exact output so any accidental change to the construction is caught.
    /// </summary>
    [Fact]
    public void EncryptMatchesRegressionKnownAnswerVector()
    {
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)i;
        }

        byte[] nonce = new byte[8];
        for (int i = 0; i < nonce.Length; i++)
        {
            nonce[i] = (byte)(0x50 + i);
        }

        byte[] plaintext = System.Text.Encoding.ASCII.GetBytes("Nalix Salsa20Poly1305 regression KAT payload.");
        byte[] aad = System.Text.Encoding.ASCII.GetBytes("nalix-aad");

        byte[] expectedCiphertext = Convert.FromHexString(
            "790FD5F0DFB48006C312232671DE7A7E97B9FA4464D13250C4566AB8D2F7773E28A3B9360C276F40E91E43F3FB");
        byte[] expectedTag = Convert.FromHexString("165206A0A0A7A6B65CF5765839DDC03A");

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];

        int written = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        Assert.Equal(plaintext.Length, written);
        Assert.Equal(expectedCiphertext, ciphertext);
        Assert.Equal(expectedTag, tag);
    }

    [Fact]
    public void DecryptWhenCiphertextIsTruncatedReturnsNegativeResult()
    {
        byte[] key = RandomBytes(32);
        byte[] nonce = RandomBytes(8);
        byte[] plaintext = RandomBytes(32);
        byte[] aad = RandomBytes(4);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];
        _ = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        byte[] truncated = ciphertext.AsSpan(0, ciphertext.Length - 1).ToArray();
        byte[] recovered = new byte[truncated.Length];

        int result = Salsa20Poly1305.Decrypt(key, nonce, truncated, aad, tag, recovered);

        Assert.True(result < 0, "Decrypt should reject a truncated ciphertext.");
    }

    [Fact]
    public void DecryptWhenAadIsTamperedReturnsNegativeResult()
    {
        byte[] key = RandomBytes(32);
        byte[] nonce = RandomBytes(8);
        byte[] plaintext = RandomBytes(32);
        byte[] aad = RandomBytes(8);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];
        _ = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        aad[0] ^= 0xFF;
        byte[] recovered = new byte[plaintext.Length];

        int result = Salsa20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, recovered);

        Assert.True(result < 0, "Decrypt should reject tampered AAD.");
    }

    [Fact]
    public void DecryptWithWrongKeyReturnsNegativeResult()
    {
        byte[] key = RandomBytes(32);
        byte[] wrongKey = RandomBytes(32);
        byte[] nonce = RandomBytes(8);
        byte[] plaintext = RandomBytes(32);
        byte[] aad = RandomBytes(4);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];
        _ = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        byte[] recovered = new byte[plaintext.Length];
        int result = Salsa20Poly1305.Decrypt(wrongKey, nonce, ciphertext, aad, tag, recovered);

        Assert.True(result < 0, "Decrypt should reject the wrong key.");
    }

    [Fact]
    public void DecryptWithWrongNonceReturnsNegativeResult()
    {
        byte[] key = RandomBytes(32);
        byte[] nonce = RandomBytes(8);
        byte[] wrongNonce = RandomBytes(8);
        byte[] plaintext = RandomBytes(32);
        byte[] aad = RandomBytes(4);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[Salsa20Poly1305.TagSize];
        _ = Salsa20Poly1305.Encrypt(key, nonce, plaintext, aad, ciphertext, tag);

        byte[] recovered = new byte[plaintext.Length];
        int result = Salsa20Poly1305.Decrypt(key, wrongNonce, ciphertext, aad, tag, recovered);

        Assert.True(result < 0, "Decrypt should reject the wrong nonce.");
    }
}















