// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Shared.Security.Engine;
using Nalix.Shared.Security.Symmetric;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

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
    private const Int32 HeaderSize = 12;

    /// <summary>ChaCha20 nonce length in bytes (96-bit per RFC 7539).</summary>
    private const Int32 ChaCha20NonceSize = 12;

    /// <summary>Salsa20 nonce length in bytes (64-bit per Salsa20 spec).</summary>
    private const Int32 Salsa20NonceSize = 8;

    // =========================================================================
    //  Shared test material
    // =========================================================================

    // 32-byte all-zero key (deterministic)
    private static readonly Byte[] Key32 = new Byte[32];

    // 16-byte all-zero key (Salsa20 128-bit mode)
    private static readonly Byte[] Key16 = new Byte[16];

    // 12-byte all-zero nonce for ChaCha20
    private static readonly Byte[] Nonce12 = new Byte[12];

    // 8-byte all-zero nonce for Salsa20
    private static readonly Byte[] Nonce8 = new Byte[8];

    // Short plaintext (< 1 block = 64 bytes)
    private static readonly Byte[] PlaintextShort =
        System.Text.Encoding.UTF8.GetBytes("Hello, Nalix!");

    // Exactly 1 block (64 bytes)
    private static readonly Byte[] PlaintextOneBlock = new Byte[64];

    // Multi-block plaintext (3 full blocks + a tail)
    private static readonly Byte[] PlaintextMultiBlock = new Byte[200];

    static SymmetricEngineTests()
    {
        for (Int32 i = 0; i < PlaintextOneBlock.Length; i++)
        {
            PlaintextOneBlock[i] = (Byte)(i + 1);
        }

        for (Int32 i = 0; i < PlaintextMultiBlock.Length; i++)
        {
            PlaintextMultiBlock[i] = (Byte)(i & 0xFF);
        }
    }

    // =========================================================================
    //  Helper: hex string → byte[]
    // =========================================================================

    private static Byte[] HexToBytes(String hex)
    {
        hex = hex.Replace(" ", "").Replace("\n", "");
        Byte[] result = new Byte[hex.Length / 2];
        for (Int32 i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return result;
    }

    // =========================================================================
    //  Helper: calculate envelope buffer size without using EnvelopeFormat
    //  Layout: header(12) || nonce(nonceLen) || ciphertext(plaintextLen)
    // =========================================================================

    private static Int32 EnvelopeSize(Int32 nonceLen, Int32 plaintextLen)
        => HeaderSize + nonceLen + plaintextLen;

    // =========================================================================
    //  Helper: resolve nonce length per algorithm
    // =========================================================================

    private static Int32 NonceLength(CipherSuiteType algorithm)
        => algorithm is CipherSuiteType.CHACHA20 ? ChaCha20NonceSize : Salsa20NonceSize;

    private static Byte[] NonceFor(CipherSuiteType algorithm)
        => algorithm is CipherSuiteType.CHACHA20 ? Nonce12 : Nonce8;

    // =========================================================================
    //  1. ChaCha20 — RFC 7539 Test Vector
    // =========================================================================

    [Fact]
    public void ChaCha20_GenerateKeyBlock_RFC7539_Section2_4_2_Vector()
    {
        // RFC 7539 §2.4.2: key = 0x00..0x1f, nonce = 000000090000004a00000000, counter = 1
        // Expected keystream block 1 (first 64 bytes).
        Byte[] key = new Byte[32];
        for (Int32 i = 0; i < 32; i++)
        {
            key[i] = (Byte)i;
        }

        Byte[] nonce = HexToBytes("000000090000004a00000000");

        Byte[] expected = HexToBytes(
            "10f1e7e4d13b5915500fdd1fa32071c4" +
            "c7d1f4c733c068030422aa9ac3d46c4e" +
            "d2826446079faa0914c2d705d98b02a2" +
            "b5129cd1de164eb9cbd083e8a2503c4e");

        ChaCha20 cipher = new(key, nonce, 1u);
        Byte[] keystream = new Byte[64];
        cipher.GenerateKeyBlock(keystream);
        cipher.Clear();

        Assert.Equal(expected, keystream);
    }

    [Fact]
    public void ChaCha20_Encrypt_ShortPlaintext_RoundTrips()
    {
        Byte[] ct = ChaCha20.Encrypt(Key32, Nonce12, 0u, PlaintextShort);
        Byte[] pt = ChaCha20.Decrypt(Key32, Nonce12, 0u, ct);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void ChaCha20_Encrypt_ExactlyOneBlock_RoundTrips()
    {
        Byte[] ct = ChaCha20.Encrypt(Key32, Nonce12, 0u, PlaintextOneBlock);
        Byte[] pt = ChaCha20.Decrypt(Key32, Nonce12, 0u, ct);
        Assert.Equal(PlaintextOneBlock, pt);
    }

    [Fact]
    public void ChaCha20_Encrypt_MultiBlock_RoundTrips()
    {
        Byte[] ct = ChaCha20.Encrypt(Key32, Nonce12, 0u, PlaintextMultiBlock);
        Byte[] pt = ChaCha20.Decrypt(Key32, Nonce12, 0u, ct);
        Assert.Equal(PlaintextMultiBlock, pt);
    }

    [Fact]
    public void ChaCha20_EncryptInPlace_RoundTrips()
    {
        Byte[] buffer = (Byte[])PlaintextShort.Clone();

        ChaCha20 enc = new(Key32, Nonce12, 0u);
        enc.EncryptInPlace(buffer);
        enc.Clear();

        ChaCha20 dec = new(Key32, Nonce12, 0u);
        dec.DecryptInPlace(buffer);
        dec.Clear();

        Assert.Equal(PlaintextShort, buffer);
    }

    [Fact]
    public void ChaCha20_Ciphertext_DiffersFromPlaintext()
    {
        Byte[] ct = ChaCha20.Encrypt(Key32, Nonce12, 0u, PlaintextShort);
        Assert.NotEqual(PlaintextShort, ct);
    }

    [Fact]
    public void ChaCha20_DifferentCounters_ProduceDifferentCiphertext()
    {
        Byte[] ct0 = ChaCha20.Encrypt(Key32, Nonce12, 0u, PlaintextOneBlock);
        Byte[] ct1 = ChaCha20.Encrypt(Key32, Nonce12, 1u, PlaintextOneBlock);
        Assert.NotEqual(ct0, ct1);
    }

    [Fact]
    public void ChaCha20_DifferentNonces_ProduceDifferentCiphertext()
    {
        Byte[] nonce2 = new Byte[12]; nonce2[0] = 1;
        Byte[] ct1 = ChaCha20.Encrypt(Key32, Nonce12, 0u, PlaintextShort);
        Byte[] ct2 = ChaCha20.Encrypt(Key32, nonce2, 0u, PlaintextShort);
        Assert.NotEqual(ct1, ct2);
    }

    [Fact]
    public void ChaCha20_EmptyPlaintext_ReturnsEmptyArray()
    {
        Byte[] ct = ChaCha20.Encrypt(Key32, Nonce12, 0u, Array.Empty<Byte>());
        Assert.Empty(ct);
    }

    // =========================================================================
    //  2. Salsa20 — Known-Answer Tests
    // =========================================================================

    [Fact]
    public void Salsa20_Encrypt_ShortPlaintext_RoundTrips()
    {
        Byte[] ct = Salsa20.Encrypt(Key32, Nonce8, 0UL, PlaintextShort);
        Byte[] pt = Salsa20.Decrypt(Key32, Nonce8, 0UL, ct);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20_Encrypt_ExactlyOneBlock_RoundTrips()
    {
        Byte[] ct = Salsa20.Encrypt(Key32, Nonce8, 0UL, PlaintextOneBlock);
        Byte[] pt = Salsa20.Decrypt(Key32, Nonce8, 0UL, ct);
        Assert.Equal(PlaintextOneBlock, pt);
    }

    [Fact]
    public void Salsa20_Encrypt_MultiBlock_RoundTrips()
    {
        Byte[] ct = Salsa20.Encrypt(Key32, Nonce8, 0UL, PlaintextMultiBlock);
        Byte[] pt = Salsa20.Decrypt(Key32, Nonce8, 0UL, ct);
        Assert.Equal(PlaintextMultiBlock, pt);
    }

    [Fact]
    public void Salsa20_Encrypt_128BitKey_RoundTrips()
    {
        Byte[] ct = Salsa20.Encrypt(Key16, Nonce8, 0UL, PlaintextShort);
        Byte[] pt = Salsa20.Decrypt(Key16, Nonce8, 0UL, ct);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20_DifferentCounters_ProduceDifferentCiphertext()
    {
        Byte[] ct0 = Salsa20.Encrypt(Key32, Nonce8, 0UL, PlaintextOneBlock);
        Byte[] ct1 = Salsa20.Encrypt(Key32, Nonce8, 1UL, PlaintextOneBlock);
        Assert.NotEqual(ct0, ct1);
    }

    [Fact]
    public void Salsa20_EmptyPlaintext_ReturnsEmptyArray()
    {
        Byte[] ct = Salsa20.Encrypt(Key32, Nonce8, 0UL, Array.Empty<Byte>());
        Assert.Empty(ct);
    }

    [Fact]
    public void Salsa20_WrongKeyLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Salsa20.Encrypt(new Byte[20], Nonce8, 0UL, PlaintextShort)); // 20 is invalid
    }

    [Fact]
    public void Salsa20_WrongNonceLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Salsa20.Encrypt(Key32, new Byte[12], 0UL, PlaintextShort)); // must be 8
    }

    [Fact]
    public void Salsa20_OutputBufferTooSmall_ThrowsArgumentException()
    {
        Byte[] tooSmall = new Byte[1];
        Assert.Throws<ArgumentException>(() =>
            Salsa20.Encrypt(Key32, Nonce8, 0UL, PlaintextShort, tooSmall));
    }

    [Fact]
    public void Salsa20_SpanOverload_ReturnsCorrectByteCount()
    {
        Byte[] dst = new Byte[PlaintextShort.Length];
        Int32 written = Salsa20.Encrypt(Key32, Nonce8, 0UL, PlaintextShort, dst);
        Assert.Equal(PlaintextShort.Length, written);
    }

    // =========================================================================
    //  4. SymmetricEngine — Envelope API
    //  Buffer sizes calculated manually:
    //    envelopeSize = HeaderSize(12) + nonceLen + plaintextLen
    // =========================================================================


    [Theory]
    [InlineData(CipherSuiteType.CHACHA20)]
    [InlineData(CipherSuiteType.SALSA20)]
    public void SymmetricEngine_Envelope_MultiBlock_RoundTrips(CipherSuiteType algorithm)
    {
        Byte[] nonce = NonceFor(algorithm);
        Int32 nLen = NonceLength(algorithm);
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextMultiBlock.Length)];
        Byte[] plaintext = new Byte[PlaintextMultiBlock.Length];

        Boolean encOk = SymmetricEngine.Encrypt(
            Key32, PlaintextMultiBlock, envelope, nonce,
            seq: 99u, algorithm, out Int32 encWritten);

        Assert.True(encOk);
        Assert.Equal(EnvelopeSize(nLen, PlaintextMultiBlock.Length), encWritten);

        Boolean decOk = SymmetricEngine.Decrypt(Key32, envelope, plaintext, out Int32 decWritten);

        Assert.True(decOk);
        Assert.Equal(PlaintextMultiBlock.Length, decWritten);
        Assert.Equal(PlaintextMultiBlock, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.CHACHA20)]
    [InlineData(CipherSuiteType.SALSA20)]
    public void SymmetricEngine_Envelope_BufferTooSmall_ReturnsFalse(CipherSuiteType algorithm)
    {
        Byte[] nonce = NonceFor(algorithm);
        Byte[] tinyBuffer = new Byte[1]; // far too small

        Boolean ok = SymmetricEngine.Encrypt(
            Key32, PlaintextShort, tinyBuffer, nonce,
            seq: 0u, algorithm, out Int32 written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void SymmetricEngine_Envelope_CorruptMagicBytes_DecryptReturnsFalse()
    {
        Int32 nLen = ChaCha20NonceSize;
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] ptBuf = new Byte[PlaintextShort.Length];

        SymmetricEngine.Encrypt(
            Key32, PlaintextShort, envelope, Nonce12,
            seq: 1u, CipherSuiteType.CHACHA20, out _);

        // Corrupt the first byte of MAGIC "NALX"
        envelope[0] ^= 0xFF;

        Boolean decOk = SymmetricEngine.Decrypt(Key32, envelope, ptBuf, out Int32 decWritten);

        Assert.False(decOk);
        Assert.Equal(0, decWritten);
    }

    [Fact]
    public void SymmetricEngine_Envelope_EmptyEnvelope_DecryptReturnsFalse()
    {
        Byte[] ptBuf = new Byte[10];
        Boolean ok = SymmetricEngine.Decrypt(Key32, Array.Empty<Byte>(), ptBuf, out Int32 written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void SymmetricEngine_Envelope_TruncatedEnvelope_DecryptReturnsFalse()
    {
        Int32 nLen = ChaCha20NonceSize;
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] ptBuf = new Byte[PlaintextShort.Length];

        SymmetricEngine.Encrypt(
            Key32, PlaintextShort, envelope, Nonce12,
            seq: 5u, CipherSuiteType.CHACHA20, out _);

        // Truncate to just the header (missing nonce + ciphertext)
        Byte[] truncated = envelope[..HeaderSize];

        Boolean ok = SymmetricEngine.Decrypt(Key32, truncated, ptBuf, out Int32 written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    // =========================================================================
    //  5. Cross-algorithm isolation
    // =========================================================================
}