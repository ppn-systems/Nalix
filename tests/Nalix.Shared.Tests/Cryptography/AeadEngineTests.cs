// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Shared.Security.Aead;
using Nalix.Shared.Security.Engine;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

/// <summary>
/// Unit tests for <see cref="AeadEngine"/>, <see cref="ChaCha20Poly1305"/>,
/// and <see cref="Salsa20Poly1305"/>.
/// </summary>
/// <remarks>
/// Envelope layout (hand-calculated, no EnvelopeFormat import):
///   header(12) || nonce(nonceLen) || ciphertext(ptLen) || tag(16)
/// </remarks>
public sealed class AeadEngineTests
{
    // =========================================================================
    //  Layout constants — hand-calculated, NO EnvelopeFormat reference
    // =========================================================================

    private const Int32 HeaderSize = 12;
    private const Int32 TagSize = 16;
    private const Int32 ChaCha20NonceLen = 12;
    private const Int32 Salsa20NonceLen = 8;

    // =========================================================================
    //  Shared test material
    // =========================================================================

    private static readonly Byte[] Key32 = new Byte[32];
    private static readonly Byte[] Key16 = new Byte[16];
    private static readonly Byte[] Nonce12 = new Byte[12];
    private static readonly Byte[] Nonce8 = new Byte[8];

    private static readonly Byte[] PlaintextShort = System.Text.Encoding.UTF8.GetBytes("Hello, Nalix AEAD!");
    private static readonly Byte[] PlaintextOneBlock = new Byte[64];
    private static readonly Byte[] PlaintextMulti = new Byte[200];
    private static readonly Byte[] Aad = System.Text.Encoding.UTF8.GetBytes("nalix-aad-header");

    static AeadEngineTests()
    {
        for (Int32 i = 0; i < PlaintextOneBlock.Length; i++)
        {
            PlaintextOneBlock[i] = (Byte)(i + 1);
        }

        for (Int32 i = 0; i < PlaintextMulti.Length; i++)
        {
            PlaintextMulti[i] = (Byte)(i & 0xFF);
        }
    }

    // =========================================================================
    //  Helpers — no EnvelopeFormat import
    // =========================================================================

    /// <summary>Envelope buffer size: header(12) + nonce + ciphertext + tag(16).</summary>
    private static Int32 EnvelopeSize(Int32 nonceLen, Int32 ptLen)
        => HeaderSize + nonceLen + ptLen + TagSize;

    private static Int32 NonceLen(CipherSuiteType alg)
        => alg is CipherSuiteType.CHACHA20_POLY1305 ? ChaCha20NonceLen : Salsa20NonceLen;

    private static Byte[] NonceFor(CipherSuiteType alg)
        => alg is CipherSuiteType.CHACHA20_POLY1305 ? Nonce12 : Nonce8;

    // =========================================================================
    //  1. ChaCha20Poly1305 — Span API (detached)
    // =========================================================================

    [Fact]
    public void ChaCha20Poly1305_Encrypt_ProducesCiphertextAndTag()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];

        Int32 written = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);

        Assert.Equal(PlaintextShort.Length, written);
        Assert.NotEqual(new Byte[TagSize], tag);
        Assert.NotEqual(PlaintextShort, ct);
    }

    [Fact]
    public void ChaCha20Poly1305_EncryptDecrypt_ShortPlaintext_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);
        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void ChaCha20Poly1305_EncryptDecrypt_OneBlock_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextOneBlock.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextOneBlock.Length];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextOneBlock, Aad, ct, tag);
        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextOneBlock, pt);
    }

    [Fact]
    public void ChaCha20Poly1305_EncryptDecrypt_MultiBlock_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextMulti.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextMulti.Length];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextMulti, Aad, ct, tag);
        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextMulti, pt);
    }

    [Fact]
    public void ChaCha20Poly1305_EncryptDecrypt_EmptyAad_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, ReadOnlySpan<Byte>.Empty, ct, tag);
        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, ReadOnlySpan<Byte>.Empty, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void ChaCha20Poly1305_EncryptDecrypt_EmptyPlaintext_RoundTrips()
    {
        Byte[] ct = Array.Empty<Byte>();
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = Array.Empty<Byte>();

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, ReadOnlySpan<Byte>.Empty, Aad, ct, tag);
        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        // ok == 0 for empty plaintext → still valid (>= 0)
        Assert.True(ok >= 0);
    }

    [Fact]
    public void ChaCha20Poly1305_Decrypt_TamperedCiphertext_ReturnsNegative()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);
        ct[0] ^= 0xFF;

        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305_Decrypt_TamperedTag_ReturnsNegative()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);
        tag[0] ^= 0x01;

        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305_Decrypt_WrongAad_ReturnsNegative()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);

        Int32 ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305_DifferentNonces_ProduceDifferentCiphertext()
    {
        Byte[] ct1 = new Byte[PlaintextShort.Length];
        Byte[] tag1 = new Byte[TagSize];
        Byte[] ct2 = new Byte[PlaintextShort.Length];
        Byte[] tag2 = new Byte[TagSize];
        Byte[] nonce2 = new Byte[12]; nonce2[0] = 1;

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct1, tag1);
        ChaCha20Poly1305.Encrypt(Key32, nonce2, PlaintextShort, Aad, ct2, tag2);

        Assert.NotEqual(ct1, ct2);
        Assert.NotEqual(tag1, tag2);
    }

    [Fact]
    public void ChaCha20Poly1305_WrongKeyLength_Throws()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];

        Assert.ThrowsAny<Exception>(() =>
            ChaCha20Poly1305.Encrypt(new Byte[16], Nonce12, PlaintextShort, Aad, ct, tag));
    }

    [Fact]
    public void ChaCha20Poly1305_WrongNonceLength_Throws()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];

        Assert.ThrowsAny<Exception>(() =>
            ChaCha20Poly1305.Encrypt(Key32, new Byte[8], PlaintextShort, Aad, ct, tag));
    }

    // =========================================================================
    //  2. ChaCha20Poly1305 — byte[] convenience API
    // =========================================================================

    [Fact]
    public void ChaCha20Poly1305_Array_EncryptDecrypt_RoundTrips()
    {
        Byte[] cipherWithTag = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad);
        Assert.Equal(PlaintextShort.Length + TagSize, cipherWithTag.Length);

        Byte[] pt = ChaCha20Poly1305.Decrypt(Key32, Nonce12, cipherWithTag, Aad);
        Assert.Equal(PlaintextShort, pt);
    }

    // =========================================================================
    //  3. Salsa20Poly1305 — Span API (detached)
    // =========================================================================

    [Fact]
    public void Salsa20Poly1305_Encrypt_ProducesCiphertextAndTag()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];

        Int32 written = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);

        Assert.Equal(PlaintextShort.Length, written);
        Assert.NotEqual(new Byte[TagSize], tag);
        Assert.NotEqual(PlaintextShort, ct);
    }

    [Fact]
    public void Salsa20Poly1305_EncryptDecrypt_ShortPlaintext_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);
        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305_EncryptDecrypt_OneBlock_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextOneBlock.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextOneBlock.Length];

        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextOneBlock, Aad, ct, tag);
        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextOneBlock, pt);
    }

    [Fact]
    public void Salsa20Poly1305_EncryptDecrypt_MultiBlock_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextMulti.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextMulti.Length];

        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextMulti, Aad, ct, tag);
        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextMulti, pt);
    }

    [Fact]
    public void Salsa20Poly1305_EncryptDecrypt_128BitKey_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        Salsa20Poly1305.Encrypt(Key16, Nonce8, PlaintextShort, Aad, ct, tag);
        Int32 ok = Salsa20Poly1305.Decrypt(Key16, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305_EncryptDecrypt_EmptyAad_RoundTrips()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, ReadOnlySpan<Byte>.Empty, ct, tag);
        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, ReadOnlySpan<Byte>.Empty, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305_EncryptDecrypt_EmptyPlaintext_RoundTrips()
    {
        Byte[] ct = Array.Empty<Byte>();
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = Array.Empty<Byte>();

        Salsa20Poly1305.Encrypt(Key32, Nonce8, ReadOnlySpan<Byte>.Empty, Aad, ct, tag);
        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
    }

    [Fact]
    public void Salsa20Poly1305_Decrypt_TamperedCiphertext_ReturnsNegative()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);
        ct[0] ^= 0xFF;

        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305_Decrypt_TamperedTag_ReturnsNegative()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);
        tag[7] ^= 0x01;

        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305_Decrypt_WrongAad_ReturnsNegative()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];
        Byte[] pt = new Byte[PlaintextShort.Length];

        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);

        Int32 ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305_WrongKeyLength_Throws()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];

        Assert.ThrowsAny<Exception>(() =>
            Salsa20Poly1305.Encrypt(new Byte[20], Nonce8, PlaintextShort, Aad, ct, tag));
    }

    [Fact]
    public void Salsa20Poly1305_WrongNonceLength_Throws()
    {
        Byte[] ct = new Byte[PlaintextShort.Length];
        Byte[] tag = new Byte[TagSize];

        Assert.ThrowsAny<Exception>(() =>
            Salsa20Poly1305.Encrypt(Key32, new Byte[12], PlaintextShort, Aad, ct, tag));
    }

    // =========================================================================
    //  4. Salsa20Poly1305 — byte[] convenience API
    // =========================================================================

    [Fact]
    public void Salsa20Poly1305_Array_EncryptDecrypt_RoundTrips()
    {
        Byte[] cipherWithTag = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad);
        Assert.Equal(PlaintextShort.Length + TagSize, cipherWithTag.Length);

        Byte[] pt = Salsa20Poly1305.Decrypt(Key32, Nonce8, cipherWithTag, Aad);
        Assert.Equal(PlaintextShort, pt);
    }

    // =========================================================================
    //  5. AeadEngine — Envelope API
    // =========================================================================

    [Theory]
    [InlineData(CipherSuiteType.CHACHA20_POLY1305)]
    [InlineData(CipherSuiteType.SALSA20_POLY1305)]
    public void AeadEngine_Envelope_EncryptDecrypt_RoundTrips(CipherSuiteType algorithm)
    {
        Byte[] nonce = NonceFor(algorithm);
        Int32 nLen = NonceLen(algorithm);
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] plaintext = new Byte[PlaintextShort.Length];

        Boolean encOk = AeadEngine.Encrypt(
            Key32, PlaintextShort, envelope, nonce,
            Aad, seq: 1u, algorithm, out Int32 encWritten);

        Assert.True(encOk);
        Assert.Equal(EnvelopeSize(nLen, PlaintextShort.Length), encWritten);

        Boolean decOk = AeadEngine.Decrypt(
            Key32, envelope[..encWritten], plaintext, Aad, out Int32 decWritten);

        Assert.True(decOk);
        Assert.Equal(PlaintextShort.Length, decWritten);
        Assert.Equal(PlaintextShort, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.CHACHA20_POLY1305)]
    [InlineData(CipherSuiteType.SALSA20_POLY1305)]
    public void AeadEngine_Envelope_EmptyPlaintext_RoundTrips(CipherSuiteType algorithm)
    {
        Byte[] nonce = NonceFor(algorithm);
        Int32 nLen = NonceLen(algorithm);
        Byte[] envelope = new Byte[EnvelopeSize(nLen, 0)];

        Boolean encOk = AeadEngine.Encrypt(
            Key32, ReadOnlySpan<Byte>.Empty, envelope, nonce,
            Aad, seq: 0u, algorithm, out Int32 encWritten);

        Assert.True(encOk);
        Assert.Equal(EnvelopeSize(nLen, 0), encWritten);

        Boolean decOk = AeadEngine.Decrypt(
            Key32, envelope[..encWritten], Span<Byte>.Empty, Aad, out Int32 decWritten);

        Assert.True(decOk);
        Assert.Equal(0, decWritten);
    }

    [Theory]
    [InlineData(CipherSuiteType.CHACHA20_POLY1305)]
    [InlineData(CipherSuiteType.SALSA20_POLY1305)]
    public void AeadEngine_Envelope_MultiBlock_RoundTrips(CipherSuiteType algorithm)
    {
        Byte[] nonce = NonceFor(algorithm);
        Int32 nLen = NonceLen(algorithm);
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextMulti.Length)];
        Byte[] plaintext = new Byte[PlaintextMulti.Length];

        Boolean encOk = AeadEngine.Encrypt(
            Key32, PlaintextMulti, envelope, nonce,
            Aad, seq: 99u, algorithm, out Int32 encWritten);

        Assert.True(encOk);

        Boolean decOk = AeadEngine.Decrypt(
            Key32, envelope[..encWritten], plaintext, Aad, out Int32 decWritten);

        Assert.True(decOk);
        Assert.Equal(PlaintextMulti.Length, decWritten);
        Assert.Equal(PlaintextMulti, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.CHACHA20_POLY1305)]
    [InlineData(CipherSuiteType.SALSA20_POLY1305)]
    public void AeadEngine_Envelope_AutoGenerateSeq_RoundTrips(CipherSuiteType algorithm)
    {
        Byte[] nonce = NonceFor(algorithm);
        Int32 nLen = NonceLen(algorithm);
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] plaintext = new Byte[PlaintextShort.Length];

        Boolean encOk = AeadEngine.Encrypt(
            Key32, PlaintextShort, envelope, nonce,
            Aad, seq: null, algorithm, out Int32 encWritten);

        // Explicit message để biết ĐÚNG bước nào fail
        Assert.True(encOk,
            $"[{algorithm}] Encrypt failed. encWritten={encWritten}");

        Boolean decOk = AeadEngine.Decrypt(
            Key32, envelope[..encWritten], plaintext, Aad, out Int32 decWritten);

        Assert.True(decOk,
            $"[{algorithm}] Decrypt failed. encWritten={encWritten}, " +
            $"envelopeLen={envelope.Length}, plaintextBufLen={plaintext.Length}");

        Assert.Equal(PlaintextShort.Length, decWritten);
        Assert.Equal(PlaintextShort, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.CHACHA20_POLY1305)]
    [InlineData(CipherSuiteType.SALSA20_POLY1305)]
    public void AeadEngine_Envelope_BufferTooSmall_ReturnsFalse(CipherSuiteType algorithm)
    {
        Byte[] nonce = NonceFor(algorithm);
        Byte[] tinyBuffer = new Byte[1];

        Boolean ok = AeadEngine.Encrypt(
            Key32, PlaintextShort, tinyBuffer, nonce,
            Aad, seq: 0u, algorithm, out Int32 written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void AeadEngine_Envelope_CorruptMagicBytes_DecryptReturnsFalse()
    {
        Int32 nLen = ChaCha20NonceLen;
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] ptBuf = new Byte[PlaintextShort.Length];

        AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 1u, CipherSuiteType.CHACHA20_POLY1305, out _);

        envelope[0] ^= 0xFF;

        Boolean decOk = AeadEngine.Decrypt(Key32, envelope, ptBuf, Aad, out Int32 decWritten);

        Assert.False(decOk);
        Assert.Equal(0, decWritten);
    }

    [Fact]
    public void AeadEngine_Envelope_TamperedCiphertext_DecryptReturnsFalse()
    {
        Int32 nLen = ChaCha20NonceLen;
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] ptBuf = new Byte[PlaintextShort.Length];

        AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 1u, CipherSuiteType.CHACHA20_POLY1305, out Int32 encWritten);

        // FIX: only corrupt if ciphertext region is non-empty
        // Ciphertext starts at: HeaderSize + nonceLen
        // Ciphertext region: [HeaderSize + nonceLen .. HeaderSize + nonceLen + ptLen)
        // Use encWritten to guard against empty ciphertext
        Assert.True(encWritten > HeaderSize + nLen + TagSize, "Plaintext must be non-empty for this test");

        Int32 ctOffset = HeaderSize + nLen; // first byte of ciphertext in envelope
        envelope[ctOffset] ^= 0x01;

        Boolean decOk = AeadEngine.Decrypt(
            Key32, envelope[..encWritten], ptBuf, Aad, out Int32 decWritten);

        Assert.False(decOk);
    }

    [Fact]
    public void AeadEngine_Envelope_TamperedTag_DecryptReturnsFalse()
    {
        Int32 nLen = ChaCha20NonceLen;
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] ptBuf = new Byte[PlaintextShort.Length];

        Boolean encOk = AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 5u, CipherSuiteType.CHACHA20_POLY1305, out Int32 encWritten);

        Assert.True(encOk, "Encrypt must succeed for tamper-tag test");
        Assert.True(encWritten >= HeaderSize + nLen + PlaintextShort.Length + TagSize, "Envelope length must include tag");

        envelope[encWritten - 1] ^= 0xFF; // last byte = last byte of tag

        Boolean decOk = AeadEngine.Decrypt(
            Key32, envelope[..encWritten], ptBuf, Aad, out _);

        Assert.False(decOk);
    }

    [Fact]
    public void AeadEngine_Envelope_WrongAad_DecryptReturnsFalse()
    {
        Int32 nLen = ChaCha20NonceLen;
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] ptBuf = new Byte[PlaintextShort.Length];

        AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 3u, CipherSuiteType.CHACHA20_POLY1305, out Int32 encWritten);

        Boolean decOk = AeadEngine.Decrypt(
            Key32, envelope[..encWritten], ptBuf,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), out _);

        Assert.False(decOk);
    }

    [Fact]
    public void AeadEngine_Envelope_EmptyEnvelope_DecryptReturnsFalse()
    {
        Byte[] ptBuf = new Byte[10];
        Boolean ok = AeadEngine.Decrypt(Key32, Array.Empty<Byte>(), ptBuf, Aad, out Int32 written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void AeadEngine_Envelope_TruncatedToHeaderOnly_DecryptReturnsFalse()
    {
        Int32 nLen = ChaCha20NonceLen;
        Byte[] envelope = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] ptBuf = new Byte[PlaintextShort.Length];

        AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 2u, CipherSuiteType.CHACHA20_POLY1305, out _);

        Boolean ok = AeadEngine.Decrypt(
            Key32, envelope[..HeaderSize], ptBuf, Aad, out Int32 written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    // =========================================================================
    //  6. Cross-suite isolation
    // =========================================================================

    [Fact]
    public void AeadEngine_ChaCha20Poly1305_And_Salsa20Poly1305_ProduceDifferentOutput()
    {
        Byte[] ct1 = new Byte[PlaintextShort.Length];
        Byte[] tag1 = new Byte[TagSize];
        Byte[] ct2 = new Byte[PlaintextShort.Length];
        Byte[] tag2 = new Byte[TagSize];

        ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct1, tag1);
        Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct2, tag2);

        Assert.NotEqual(ct1, ct2);
        Assert.NotEqual(tag1, tag2);
    }

    [Theory]
    [InlineData(CipherSuiteType.CHACHA20_POLY1305)]
    [InlineData(CipherSuiteType.SALSA20_POLY1305)]
    public void AeadEngine_DifferentKeys_ProduceDifferentCiphertext(CipherSuiteType algorithm)
    {
        // FIX: compare only the ciphertext+tag region (skip the header which is identical)
        // Envelope header contains: magic, version, type, flags, nonceLen, seq → same for both
        // Ciphertext region starts at: HeaderSize + nonceLen
        Byte[] nonce = NonceFor(algorithm);
        Int32 nLen = NonceLen(algorithm);
        Int32 ctStart = HeaderSize + nLen; // start of ciphertext in envelope

        Byte[] env1 = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] env2 = new Byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        Byte[] key2 = new Byte[32]; key2[0] = 0xAB;

        Boolean ok1 = AeadEngine.Encrypt(Key32, PlaintextShort, env1, nonce, Aad, seq: 1u, algorithm, out Int32 w1);
        Boolean ok2 = AeadEngine.Encrypt(key2, PlaintextShort, env2, nonce, Aad, seq: 1u, algorithm, out Int32 w2);

        Assert.True(ok1, "Encrypt with Key32 must succeed");
        Assert.True(ok2, "Encrypt with key2 must succeed");

        // Compare only ciphertext+tag (bytes after header+nonce) — these MUST differ
        Byte[] ct1 = env1[ctStart..w1];
        Byte[] ct2 = env2[ctStart..w2];

        Assert.NotEqual(ct1, ct2);
    }
}