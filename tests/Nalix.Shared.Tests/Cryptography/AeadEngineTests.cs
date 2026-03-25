// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using System;
using Nalix.Common.Security;
using Nalix.Shared.Security.Aead;
using Nalix.Shared.Security.Engine;
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

    private const int HeaderSize = 12;
    private const int TagSize = 16;
    private const int ChaCha20NonceLen = 12;
    private const int Salsa20NonceLen = 8;

    // =========================================================================
    //  Shared test material
    // =========================================================================

    private static readonly byte[] Key32 = new byte[32];
    private static readonly byte[] Key16 = new byte[16];
    private static readonly byte[] Nonce12 = new byte[12];
    private static readonly byte[] Nonce8 = new byte[8];

    private static readonly byte[] PlaintextShort = System.Text.Encoding.UTF8.GetBytes("Hello, Nalix AEAD!");
    private static readonly byte[] PlaintextOneBlock = new byte[64];
    private static readonly byte[] PlaintextMulti = new byte[200];
    private static readonly byte[] Aad = System.Text.Encoding.UTF8.GetBytes("nalix-aad-header");

    static AeadEngineTests()
    {
        for (int i = 0; i < PlaintextOneBlock.Length; i++)
        {
            PlaintextOneBlock[i] = (byte)(i + 1);
        }

        for (int i = 0; i < PlaintextMulti.Length; i++)
        {
            PlaintextMulti[i] = (byte)(i & 0xFF);
        }
    }

    // =========================================================================
    //  Helpers — no EnvelopeFormat import
    // =========================================================================

    /// <summary>Envelope buffer size: header(12) + nonce + ciphertext + tag(16).</summary>
    private static int EnvelopeSize(int nonceLen, int ptLen)
        => HeaderSize + nonceLen + ptLen + TagSize;

    private static int NonceLen(CipherSuiteType alg)
        => alg is CipherSuiteType.Chacha20Poly1305 ? ChaCha20NonceLen : Salsa20NonceLen;

    private static byte[] NonceFor(CipherSuiteType alg)
        => alg is CipherSuiteType.Chacha20Poly1305 ? Nonce12 : Nonce8;

    // =========================================================================
    //  1. ChaCha20Poly1305 — Span API (detached)
    // =========================================================================

    [Fact]
    public void ChaCha20Poly1305EncryptProducesCiphertextAndTag()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];

        int written = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);

        Assert.Equal(PlaintextShort.Length, written);
        Assert.NotEqual(new byte[TagSize], tag);
        Assert.NotEqual(PlaintextShort, ct);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptShortPlaintextRoundTrips()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptOneBlockRoundTrips()
    {
        byte[] ct = new byte[PlaintextOneBlock.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextOneBlock.Length];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextOneBlock, Aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextOneBlock, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptMultiBlockRoundTrips()
    {
        byte[] ct = new byte[PlaintextMulti.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextMulti.Length];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextMulti, Aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextMulti, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptEmptyAadRoundTrips()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, [], ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, [], tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptEmptyPlaintextRoundTrips()
    {
        byte[] ct = [];
        byte[] tag = new byte[TagSize];
        byte[] pt = [];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, [], Aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);

        // ok == 0 for empty plaintext → still valid (>= 0)
        Assert.True(ok >= 0);
    }

    [Fact]
    public void ChaCha20Poly1305DecryptTamperedCiphertextReturnsNegative()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);
        ct[0] ^= 0xFF;

        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305DecryptTamperedTagReturnsNegative()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);
        tag[0] ^= 0x01;

        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305DecryptWrongAadReturnsNegative()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct, tag);

        int ok = ChaCha20Poly1305.Decrypt(Key32, Nonce12, ct,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305DifferentNoncesProduceDifferentCiphertext()
    {
        byte[] ct1 = new byte[PlaintextShort.Length];
        byte[] tag1 = new byte[TagSize];
        byte[] ct2 = new byte[PlaintextShort.Length];
        byte[] tag2 = new byte[TagSize];
        byte[] nonce2 = new byte[12]; nonce2[0] = 1;

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct1, tag1);
        _ = ChaCha20Poly1305.Encrypt(Key32, nonce2, PlaintextShort, Aad, ct2, tag2);

        Assert.NotEqual(ct1, ct2);
        Assert.NotEqual(tag1, tag2);
    }

    [Fact]
    public void ChaCha20Poly1305WrongKeyLengthThrows()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            ChaCha20Poly1305.Encrypt(new byte[16], Nonce12, PlaintextShort, Aad, ct, tag));
    }

    [Fact]
    public void ChaCha20Poly1305WrongNonceLengthThrows()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            ChaCha20Poly1305.Encrypt(Key32, new byte[8], PlaintextShort, Aad, ct, tag));
    }

    // =========================================================================
    //  3. Salsa20Poly1305 — Span API (detached)
    // =========================================================================

    [Fact]
    public void Salsa20Poly1305EncryptProducesCiphertextAndTag()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];

        int written = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);

        Assert.Equal(PlaintextShort.Length, written);
        Assert.NotEqual(new byte[TagSize], tag);
        Assert.NotEqual(PlaintextShort, ct);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptShortPlaintextRoundTrips()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptOneBlockRoundTrips()
    {
        byte[] ct = new byte[PlaintextOneBlock.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextOneBlock.Length];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextOneBlock, Aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextOneBlock, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptMultiBlockRoundTrips()
    {
        byte[] ct = new byte[PlaintextMulti.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextMulti.Length];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextMulti, Aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextMulti, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecrypt128BitKeyRoundTrips()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(Key16, Nonce8, PlaintextShort, Aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(Key16, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptEmptyAadRoundTrips()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, [], ct, tag);
        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, [], tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(PlaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptEmptyPlaintextRoundTrips()
    {
        byte[] ct = [];
        byte[] tag = new byte[TagSize];
        byte[] pt = [];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, [], Aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);

        Assert.True(ok >= 0);
    }

    [Fact]
    public void Salsa20Poly1305DecryptTamperedCiphertextReturnsNegative()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);
        ct[0] ^= 0xFF;

        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305DecryptTamperedTagReturnsNegative()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);
        tag[7] ^= 0x01;

        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct, Aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305DecryptWrongAadReturnsNegative()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[PlaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct, tag);

        int ok = Salsa20Poly1305.Decrypt(Key32, Nonce8, ct,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305WrongKeyLengthThrows()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            Salsa20Poly1305.Encrypt(new byte[20], Nonce8, PlaintextShort, Aad, ct, tag));
    }

    [Fact]
    public void Salsa20Poly1305WrongNonceLengthThrows()
    {
        byte[] ct = new byte[PlaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            Salsa20Poly1305.Encrypt(Key32, new byte[12], PlaintextShort, Aad, ct, tag));
    }
    // =========================================================================
    //  5. AeadEngine — Envelope API
    // =========================================================================

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeEncryptDecryptRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] plaintext = new byte[PlaintextShort.Length];

        bool encOk = AeadEngine.Encrypt(
            Key32, PlaintextShort, envelope, nonce,
            Aad, seq: 1u, algorithm, out int encWritten);

        Assert.True(encOk);
        Assert.Equal(EnvelopeSize(nLen, PlaintextShort.Length), encWritten);

        bool decOk = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..encWritten], plaintext, Aad, out int decWritten);

        Assert.True(decOk);
        Assert.Equal(PlaintextShort.Length, decWritten);
        Assert.Equal(PlaintextShort, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeEmptyPlaintextRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, 0)];

        bool encOk = AeadEngine.Encrypt(
            Key32, [], envelope, nonce,
            Aad, seq: 0u, algorithm, out int encWritten);

        Assert.True(encOk);
        Assert.Equal(EnvelopeSize(nLen, 0), encWritten);

        bool decOk = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..encWritten], [], Aad, out int decWritten);

        Assert.True(decOk);
        Assert.Equal(0, decWritten);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeMultiBlockRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextMulti.Length)];
        byte[] plaintext = new byte[PlaintextMulti.Length];

        bool encOk = AeadEngine.Encrypt(
            Key32, PlaintextMulti, envelope, nonce,
            Aad, seq: 99u, algorithm, out int encWritten);

        Assert.True(encOk);

        bool decOk = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..encWritten], plaintext, Aad, out int decWritten);

        Assert.True(decOk);
        Assert.Equal(PlaintextMulti.Length, decWritten);
        Assert.Equal(PlaintextMulti, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeAutoGenerateSeqRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] plaintext = new byte[PlaintextShort.Length];

        bool encOk = AeadEngine.Encrypt(
            Key32, PlaintextShort, envelope, nonce,
            Aad, seq: null, algorithm, out int encWritten);

        // Explicit message để biết ĐÚNG bước nào fail
        Assert.True(encOk,
            $"[{algorithm}] Encrypt failed. encWritten={encWritten}");

        bool decOk = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..encWritten], plaintext, Aad, out int decWritten);

        Assert.True(decOk,
            $"[{algorithm}] Decrypt failed. encWritten={encWritten}, " +
            $"envelopeLen={envelope.Length}, plaintextBufLen={plaintext.Length}");

        Assert.Equal(PlaintextShort.Length, decWritten);
        Assert.Equal(PlaintextShort, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeBufferTooSmallReturnsFalse(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        byte[] tinyBuffer = new byte[1];

        bool ok = AeadEngine.Encrypt(
            Key32, PlaintextShort, tinyBuffer, nonce,
            Aad, seq: 0u, algorithm, out int written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void AeadEngineEnvelopeCorruptMagicBytesDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] ptBuf = new byte[PlaintextShort.Length];

        _ = AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 1u, CipherSuiteType.Chacha20Poly1305, out _);

        envelope[0] ^= 0xFF;

        bool decOk = AeadEngine.Decrypt(Key32, envelope, ptBuf, Aad, out int decWritten);

        Assert.False(decOk);
        Assert.Equal(0, decWritten);
    }

    [Fact]
    public void AeadEngineEnvelopeTamperedCiphertextDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] ptBuf = new byte[PlaintextShort.Length];

        _ = AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 1u, CipherSuiteType.Chacha20Poly1305, out int encWritten);

        // FIX: only corrupt if ciphertext region is non-empty
        // Ciphertext starts at: HeaderSize + nonceLen
        // Ciphertext region: [HeaderSize + nonceLen .. HeaderSize + nonceLen + ptLen)
        // Use encWritten to guard against empty ciphertext
        Assert.True(encWritten > HeaderSize + nLen + TagSize, "Plaintext must be non-empty for this test");

        int ctOffset = HeaderSize + nLen; // first byte of ciphertext in envelope
        envelope[ctOffset] ^= 0x01;
        bool decOk = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..encWritten], ptBuf, Aad, out _);

        Assert.False(decOk);
    }

    [Fact]
    public void AeadEngineEnvelopeTamperedTagDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] ptBuf = new byte[PlaintextShort.Length];

        bool encOk = AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 5u, CipherSuiteType.Chacha20Poly1305, out int encWritten);

        Assert.True(encOk, "Encrypt must succeed for tamper-tag test");
        Assert.True(encWritten >= HeaderSize + nLen + PlaintextShort.Length + TagSize, "Envelope length must include tag");

        envelope[encWritten - 1] ^= 0xFF; // last byte = last byte of tag

        bool decOk = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..encWritten], ptBuf, Aad, out _);

        Assert.False(decOk);
    }

    [Fact]
    public void AeadEngineEnvelopeWrongAadDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] ptBuf = new byte[PlaintextShort.Length];

        _ = AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 3u, CipherSuiteType.Chacha20Poly1305, out int encWritten);

        bool decOk = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..encWritten], ptBuf,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), out _);

        Assert.False(decOk);
    }

    [Fact]
    public void AeadEngineEnvelopeEmptyEnvelopeDecryptReturnsFalse()
    {
        byte[] ptBuf = new byte[10];
        bool ok = AeadEngine.Decrypt(Key32, [], ptBuf, Aad, out int written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    [Fact]
    public void AeadEngineEnvelopeTruncatedToHeaderOnlyDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] ptBuf = new byte[PlaintextShort.Length];

        _ = AeadEngine.Encrypt(Key32, PlaintextShort, envelope, Nonce12,
            Aad, seq: 2u, CipherSuiteType.Chacha20Poly1305, out _);

        bool ok = AeadEngine.Decrypt(
            Key32, envelope.AsSpan()[..HeaderSize], ptBuf, Aad, out int written);

        Assert.False(ok);
        Assert.Equal(0, written);
    }

    // =========================================================================
    //  6. Cross-suite isolation
    // =========================================================================

    [Fact]
    public void AeadEngineChaCha20Poly1305AndSalsa20Poly1305ProduceDifferentOutput()
    {
        byte[] ct1 = new byte[PlaintextShort.Length];
        byte[] tag1 = new byte[TagSize];
        byte[] ct2 = new byte[PlaintextShort.Length];
        byte[] tag2 = new byte[TagSize];

        _ = ChaCha20Poly1305.Encrypt(Key32, Nonce12, PlaintextShort, Aad, ct1, tag1);
        _ = Salsa20Poly1305.Encrypt(Key32, Nonce8, PlaintextShort, Aad, ct2, tag2);

        Assert.NotEqual(ct1, ct2);
        Assert.NotEqual(tag1, tag2);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineDifferentKeysProduceDifferentCiphertext(CipherSuiteType algorithm)
    {
        // FIX: compare only the ciphertext+tag region (skip the header which is identical)
        // Envelope header contains: magic, version, type, flags, nonceLen, seq → same for both
        // Ciphertext region starts at: HeaderSize + nonceLen
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        int ctStart = HeaderSize + nLen; // start of ciphertext in envelope

        byte[] env1 = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] env2 = new byte[EnvelopeSize(nLen, PlaintextShort.Length)];
        byte[] key2 = new byte[32]; key2[0] = 0xAB;

        bool ok1 = AeadEngine.Encrypt(Key32, PlaintextShort, env1, nonce, Aad, seq: 1u, algorithm, out int w1);
        bool ok2 = AeadEngine.Encrypt(key2, PlaintextShort, env2, nonce, Aad, seq: 1u, algorithm, out int w2);

        Assert.True(ok1, "Encrypt with Key32 must succeed");
        Assert.True(ok2, "Encrypt with key2 must succeed");

        // Compare only ciphertext+tag (bytes after header+nonce) — these MUST differ
        byte[] ct1 = env1[ctStart..w1];
        byte[] ct2 = env2[ctStart..w2];

        Assert.NotEqual(ct1, ct2);
    }
}
