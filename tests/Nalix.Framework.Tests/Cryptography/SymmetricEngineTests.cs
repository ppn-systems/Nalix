// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using System;
using Nalix.Common.Exceptions;
using Nalix.Common.Security;
using Nalix.Framework.Security.Engine;
using Nalix.Framework.Security.Symmetric;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// Unit tests for <see cref="SymmetricEngine"/> covering the raw keystream API
/// and the envelope (header || nonce || ciphertext) API.
/// Also covers <see cref="ChaCha20"/> and <see cref="Salsa20"/> directly
/// against RFC / spec test vectors.
/// </summary>
public sealed class SymmetricEngineTests
{
    // =========================================================================
    //  Envelope layout constants (derived from EnvelopeHeader.cs comments)
    //  Header = MAGIC(4) + version(1) + type(1) + flags(1) + nonceLen(1) + seq(4) = 12 bytes
    // =========================================================================

    /// <summary>Fixed header size in bytes (MAGIC + version + type + flags + nonceLen + seq).</summary>
    private const int HeaderSize = 12;

    /// <summary>ChaCha20 nonce length in bytes (96-bit per RFC 7539).</summary>
    private const int ChaCha20NonceSize = 12;

    /// <summary>Salsa20 nonce length in bytes (64-bit per Salsa20 spec).</summary>
    private const int Salsa20NonceSize = 8;

    // =========================================================================
    //  Shared test material
    // =========================================================================

    // 32-byte all-zero key (deterministic)
    private static readonly byte[] s_key32 = new byte[32];

    // 12-byte all-zero nonce for ChaCha20
    private static readonly byte[] s_nonce12 = new byte[12];

    // 8-byte all-zero nonce for Salsa20
    private static readonly byte[] s_nonce8 = new byte[8];

    // Short plaintext (< 1 block = 64 bytes)
    private static readonly byte[] s_plaintextShort =
        System.Text.Encoding.UTF8.GetBytes("Hello, Nalix!");

    // Exactly 1 block (64 bytes)
    private static readonly byte[] s_plaintextOneBlock = new byte[64];

    // Multi-block plaintext (3 full blocks + a tail)
    private static readonly byte[] s_plaintextMultiBlock = new byte[200];

    static SymmetricEngineTests()
    {
        for (int i = 0; i < s_plaintextOneBlock.Length; i++)
        {
            s_plaintextOneBlock[i] = (byte)(i + 1);
        }

        for (int i = 0; i < s_plaintextMultiBlock.Length; i++)
        {
            s_plaintextMultiBlock[i] = (byte)(i & 0xFF);
        }
    }

    // =========================================================================
    //  Helper: hex string → byte[]
    // =========================================================================

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("\n", "");
        byte[] result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return result;
    }

    // =========================================================================
    //  Helper: calculate envelope buffer size without using EnvelopeFormat
    //  Layout: header(12) || nonce(nonceLen) || ciphertext(plaintextLen)
    // =========================================================================

    private static int EnvelopeSize(int nonceLen, int plaintextLen)
        => HeaderSize + nonceLen + plaintextLen;

    // =========================================================================
    //  Helper: resolve nonce length per algorithm
    // =========================================================================

    private static int NonceLength(CipherSuiteType algorithm)
        => algorithm is CipherSuiteType.Chacha20 ? ChaCha20NonceSize : Salsa20NonceSize;

    private static byte[] NonceFor(CipherSuiteType algorithm)
        => algorithm is CipherSuiteType.Chacha20 ? s_nonce12 : s_nonce8;

    // =========================================================================
    //  1. ChaCha20 — RFC 7539 Test Vector
    // =========================================================================

    [Fact]
    public void ChaCha20GenerateKeyBlockRFC7539Section242Vector()
    {
        // RFC 7539 §2.4.2: key = 0x00..0x1f, nonce = 000000090000004a00000000, counter = 1
        // Expected keystream block 1 (first 64 bytes).
        byte[] key = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            key[i] = (byte)i;
        }

        byte[] nonce = HexToBytes("000000090000004a00000000");

        byte[] expected = HexToBytes(
            "10f1e7e4d13b5915500fdd1fa32071c4" +
            "c7d1f4c733c068030422aa9ac3d46c4e" +
            "d2826446079faa0914c2d705d98b02a2" +
            "b5129cd1de164eb9cbd083e8a2503c4e");

        ChaCha20 cipher = new(key, nonce, 1u);
        byte[] keystream = new byte[64];
        cipher.GenerateKeyBlock(keystream);
        cipher.Clear();

        Assert.Equal(expected, keystream);
    }

    [Fact]
    public void Salsa20OutputBufferTooSmallThrowsArgumentException()
    {
        byte[] tooSmall = new byte[1];
        _ = Assert.Throws<ArgumentException>(() =>
            Salsa20.Encrypt(s_key32, s_nonce8, 0UL, s_plaintextShort, tooSmall));
    }

    [Fact]
    public void Salsa20SpanOverloadReturnsCorrectByteCount()
    {
        byte[] dst = new byte[s_plaintextShort.Length];
        int written = Salsa20.Encrypt(s_key32, s_nonce8, 0UL, s_plaintextShort, dst);
        Assert.Equal(s_plaintextShort.Length, written);
    }

    // =========================================================================
    //  4. SymmetricEngine — Envelope API
    //  Buffer sizes calculated manually:
    //    envelopeSize = HeaderSize(12) + nonceLen + plaintextLen
    // =========================================================================


    [Theory]
    [InlineData(CipherSuiteType.Chacha20)]
    [InlineData(CipherSuiteType.Salsa20)]
    public void SymmetricEngineEnvelopeMultiBlockRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLength(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextMultiBlock.Length)];
        byte[] plaintext = new byte[s_plaintextMultiBlock.Length];

        SymmetricEngine.Encrypt(
            s_key32, s_plaintextMultiBlock, envelope, nonce,
            seq: 99u, algorithm, out int encWritten);
        Assert.Equal(EnvelopeSize(nLen, s_plaintextMultiBlock.Length), encWritten);

        SymmetricEngine.Decrypt(s_key32, envelope, plaintext, out int decWritten);
        Assert.Equal(s_plaintextMultiBlock.Length, decWritten);
        Assert.Equal(s_plaintextMultiBlock, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20)]
    [InlineData(CipherSuiteType.Salsa20)]
    public void SymmetricEngineEnvelopeBufferTooSmallReturnsFalse(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        byte[] tinyBuffer = new byte[1]; // far too small

        _ = Assert.Throws<ArgumentException>(() => SymmetricEngine.Encrypt(
            s_key32, s_plaintextShort, tinyBuffer, nonce,
            seq: 0u, algorithm, out _));
    }

    [Fact]
    public void SymmetricEngineEnvelopeCorruptMagicBytesDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceSize;
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] ptBuf = new byte[s_plaintextShort.Length];

        SymmetricEngine.Encrypt(
            s_key32, s_plaintextShort, envelope, s_nonce12,
            seq: 1u, CipherSuiteType.Chacha20, out _);

        // Corrupt the first byte of MAGIC "NALX"
        envelope[0] ^= 0xFF;

        _ = Assert.Throws<CipherException>(() => SymmetricEngine.Decrypt(s_key32, envelope, ptBuf, out _));
    }

    [Fact]
    public void SymmetricEngineEnvelopeEmptyEnvelopeDecryptReturnsFalse()
    {
        byte[] ptBuf = new byte[10];
        _ = Assert.Throws<CipherException>(() => SymmetricEngine.Decrypt(s_key32, [], ptBuf, out _));
    }

    [Fact]
    public void SymmetricEngineEnvelopeTruncatedEnvelopeDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceSize;
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] ptBuf = new byte[s_plaintextShort.Length];

        SymmetricEngine.Encrypt(
            s_key32, s_plaintextShort, envelope, s_nonce12,
            seq: 5u, CipherSuiteType.Chacha20, out _);

        // Truncate to just the header (missing nonce + ciphertext)
        byte[] truncated = envelope[..HeaderSize];

        _ = Assert.Throws<CipherException>(() => SymmetricEngine.Decrypt(s_key32, truncated, ptBuf, out _));
    }

    // =========================================================================
    //  5. Cross-algorithm isolation
    // =========================================================================
}
