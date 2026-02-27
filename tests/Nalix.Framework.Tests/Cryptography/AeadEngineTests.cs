// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Exceptions;
using Nalix.Common.Security;
using Nalix.Framework.Security.Aead;
using Nalix.Framework.Security.Engine;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// Verifies detached AEAD primitives and the public envelope API exposed by <see cref="AeadEngine"/>.
/// </summary>
public sealed class AeadEngineTests
{
    private const int HeaderSize = 12;
    private const int TagSize = 16;
    private const int ChaCha20NonceLen = 12;
    private const int Salsa20NonceLen = 8;

    private static readonly byte[] s_key32 = new byte[32];
    private static readonly byte[] s_key16 = new byte[16];
    private static readonly byte[] s_nonce12 = new byte[12];
    private static readonly byte[] s_nonce8 = new byte[8];

    private static readonly byte[] s_plaintextShort = System.Text.Encoding.UTF8.GetBytes("Hello, Nalix AEAD!");
    private static readonly byte[] s_plaintextOneBlock = new byte[64];
    private static readonly byte[] s_plaintextMulti = new byte[200];
    private static readonly byte[] s_aad = System.Text.Encoding.UTF8.GetBytes("nalix-aad-header");

    static AeadEngineTests()
    {
        for (int i = 0; i < s_plaintextOneBlock.Length; i++)
        {
            s_plaintextOneBlock[i] = (byte)(i + 1);
        }

        for (int i = 0; i < s_plaintextMulti.Length; i++)
        {
            s_plaintextMulti[i] = (byte)(i & 0xFF);
        }
    }

    private static int EnvelopeSize(int nonceLen, int ptLen)
        => HeaderSize + nonceLen + ptLen + TagSize;

    private static int NonceLen(CipherSuiteType alg)
        => alg is CipherSuiteType.Chacha20Poly1305 ? ChaCha20NonceLen : Salsa20NonceLen;

    private static byte[] NonceFor(CipherSuiteType alg)
        => alg is CipherSuiteType.Chacha20Poly1305 ? s_nonce12 : s_nonce8;

    [Fact]
    public void ChaCha20Poly1305EncryptProducesCiphertextAndTag()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];

        int written = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, s_aad, ct, tag);

        Assert.Equal(s_plaintextShort.Length, written);
        Assert.NotEqual(new byte[TagSize], tag);
        Assert.NotEqual(s_plaintextShort, ct);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptShortPlaintextRoundTrips()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, s_aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextShort, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptOneBlockRoundTrips()
    {
        byte[] ct = new byte[s_plaintextOneBlock.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextOneBlock.Length];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextOneBlock, s_aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextOneBlock, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptMultiBlockRoundTrips()
    {
        byte[] ct = new byte[s_plaintextMulti.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextMulti.Length];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextMulti, s_aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextMulti, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptEmptyAadRoundTrips()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, [], ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct, [], tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextShort, pt);
    }

    [Fact]
    public void ChaCha20Poly1305EncryptDecryptEmptyPlaintextRoundTrips()
    {
        byte[] ct = [];
        byte[] tag = new byte[TagSize];
        byte[] pt = [];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, [], s_aad, ct, tag);
        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
    }

    [Fact]
    public void ChaCha20Poly1305DecryptTamperedCiphertextReturnsNegative()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, s_aad, ct, tag);
        ct[0] ^= 0xFF;

        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct, s_aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305DecryptTamperedTagReturnsNegative()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, s_aad, ct, tag);
        tag[0] ^= 0x01;

        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct, s_aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305DecryptWrongAadReturnsNegative()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, s_aad, ct, tag);

        int ok = ChaCha20Poly1305.Decrypt(s_key32, s_nonce12, ct,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void ChaCha20Poly1305DifferentNoncesProduceDifferentCiphertext()
    {
        byte[] ct1 = new byte[s_plaintextShort.Length];
        byte[] tag1 = new byte[TagSize];
        byte[] ct2 = new byte[s_plaintextShort.Length];
        byte[] tag2 = new byte[TagSize];
        byte[] nonce2 = new byte[12]; nonce2[0] = 1;

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, s_aad, ct1, tag1);
        _ = ChaCha20Poly1305.Encrypt(s_key32, nonce2, s_plaintextShort, s_aad, ct2, tag2);

        Assert.NotEqual(ct1, ct2);
        Assert.NotEqual(tag1, tag2);
    }

    [Fact]
    public void ChaCha20Poly1305WrongKeyLengthThrows()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            ChaCha20Poly1305.Encrypt(new byte[16], s_nonce12, s_plaintextShort, s_aad, ct, tag));
    }

    [Fact]
    public void ChaCha20Poly1305WrongNonceLengthThrows()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            ChaCha20Poly1305.Encrypt(s_key32, new byte[8], s_plaintextShort, s_aad, ct, tag));
    }

    [Fact]
    public void Salsa20Poly1305EncryptProducesCiphertextAndTag()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];

        int written = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextShort, s_aad, ct, tag);

        Assert.Equal(s_plaintextShort.Length, written);
        Assert.NotEqual(new byte[TagSize], tag);
        Assert.NotEqual(s_plaintextShort, ct);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptShortPlaintextRoundTrips()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextShort, s_aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptOneBlockRoundTrips()
    {
        byte[] ct = new byte[s_plaintextOneBlock.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextOneBlock.Length];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextOneBlock, s_aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextOneBlock, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptMultiBlockRoundTrips()
    {
        byte[] ct = new byte[s_plaintextMulti.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextMulti.Length];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextMulti, s_aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextMulti, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecrypt128BitKeyRoundTrips()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(s_key16, s_nonce8, s_plaintextShort, s_aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(s_key16, s_nonce8, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptEmptyAadRoundTrips()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextShort, [], ct, tag);
        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct, [], tag, pt);

        Assert.True(ok >= 0);
        Assert.Equal(s_plaintextShort, pt);
    }

    [Fact]
    public void Salsa20Poly1305EncryptDecryptEmptyPlaintextRoundTrips()
    {
        byte[] ct = [];
        byte[] tag = new byte[TagSize];
        byte[] pt = [];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, [], s_aad, ct, tag);
        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct, s_aad, tag, pt);

        Assert.True(ok >= 0);
    }

    [Fact]
    public void Salsa20Poly1305DecryptTamperedCiphertextReturnsNegative()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextShort, s_aad, ct, tag);
        ct[0] ^= 0xFF;

        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct, s_aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305DecryptTamperedTagReturnsNegative()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextShort, s_aad, ct, tag);
        tag[7] ^= 0x01;

        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct, s_aad, tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305DecryptWrongAadReturnsNegative()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];
        byte[] pt = new byte[s_plaintextShort.Length];

        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextShort, s_aad, ct, tag);

        int ok = Salsa20Poly1305.Decrypt(s_key32, s_nonce8, ct,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), tag, pt);
        Assert.Equal(-1, ok);
    }

    [Fact]
    public void Salsa20Poly1305WrongKeyLengthThrows()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            Salsa20Poly1305.Encrypt(new byte[20], s_nonce8, s_plaintextShort, s_aad, ct, tag));
    }

    [Fact]
    public void Salsa20Poly1305WrongNonceLengthThrows()
    {
        byte[] ct = new byte[s_plaintextShort.Length];
        byte[] tag = new byte[TagSize];

        _ = Assert.ThrowsAny<Exception>(() =>
            Salsa20Poly1305.Encrypt(s_key32, new byte[12], s_plaintextShort, s_aad, ct, tag));
    }
    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeEncryptDecryptRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] plaintext = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(
            s_key32, s_plaintextShort, envelope, nonce,
            s_aad, seq: 1u, algorithm, out int encWritten);
        Assert.Equal(EnvelopeSize(nLen, s_plaintextShort.Length), encWritten);

        AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..encWritten], plaintext, s_aad, out int decWritten);
        Assert.Equal(s_plaintextShort.Length, decWritten);
        Assert.Equal(s_plaintextShort, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeEmptyPlaintextRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, 0)];

        AeadEngine.Encrypt(
            s_key32, [], envelope, nonce,
            s_aad, seq: 0u, algorithm, out int encWritten);
        Assert.Equal(EnvelopeSize(nLen, 0), encWritten);

        AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..encWritten], [], s_aad, out int decWritten);
        Assert.Equal(0, decWritten);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeMultiBlockRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextMulti.Length)];
        byte[] plaintext = new byte[s_plaintextMulti.Length];

        AeadEngine.Encrypt(
            s_key32, s_plaintextMulti, envelope, nonce,
            s_aad, seq: 99u, algorithm, out int encWritten);

        AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..encWritten], plaintext, s_aad, out int decWritten);
        Assert.Equal(s_plaintextMulti.Length, decWritten);
        Assert.Equal(s_plaintextMulti, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeAutoGenerateSeqRoundTrips(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] plaintext = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(
            s_key32, s_plaintextShort, envelope, nonce,
            s_aad, seq: null, algorithm, out int encWritten);

        AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..encWritten], plaintext, s_aad, out int decWritten);

        Assert.Equal(s_plaintextShort.Length, decWritten);
        Assert.Equal(s_plaintextShort, plaintext);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeBufferTooSmallReturnsFalse(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        byte[] tinyBuffer = new byte[1];

        _ = Assert.Throws<ArgumentException>(() => AeadEngine.Encrypt(
            s_key32, s_plaintextShort, tinyBuffer, nonce,
            s_aad, seq: 0u, algorithm, out _));
    }

    [Fact]
    public void AeadEngineEnvelopeCorruptMagicBytesDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] ptBuf = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(s_key32, s_plaintextShort, envelope, s_nonce12,
            s_aad, seq: 1u, CipherSuiteType.Chacha20Poly1305, out _);

        envelope[0] ^= 0xFF;

        _ = Assert.Throws<CipherException>(() => AeadEngine.Decrypt(s_key32, envelope, ptBuf, s_aad, out _));
    }

    [Fact]
    public void AeadEngineEnvelopeTamperedCiphertextDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] ptBuf = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(s_key32, s_plaintextShort, envelope, s_nonce12,
            s_aad, seq: 1u, CipherSuiteType.Chacha20Poly1305, out int encWritten);

        Assert.True(encWritten > HeaderSize + nLen + TagSize, "Plaintext must be non-empty for this test");

        int ctOffset = HeaderSize + nLen;
        envelope[ctOffset] ^= 0x01;
        _ = Assert.Throws<System.Security.Cryptography.CryptographicException>(() => AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..encWritten], ptBuf, s_aad, out _));
    }

    [Fact]
    public void AeadEngineEnvelopeTamperedTagDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] ptBuf = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(s_key32, s_plaintextShort, envelope, s_nonce12,
            s_aad, seq: 5u, CipherSuiteType.Chacha20Poly1305, out int encWritten);
        Assert.True(encWritten >= HeaderSize + nLen + s_plaintextShort.Length + TagSize, "Envelope length must include tag");

        envelope[encWritten - 1] ^= 0xFF;

        _ = Assert.Throws<System.Security.Cryptography.CryptographicException>(() => AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..encWritten], ptBuf, s_aad, out _));
    }

    [Fact]
    public void AeadEngineEnvelopeWrongAadDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] ptBuf = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(s_key32, s_plaintextShort, envelope, s_nonce12,
            s_aad, seq: 3u, CipherSuiteType.Chacha20Poly1305, out int encWritten);

        _ = Assert.Throws<System.Security.Cryptography.CryptographicException>(() => AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..encWritten], ptBuf,
            System.Text.Encoding.UTF8.GetBytes("wrong-aad"), out _));
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineEnvelopeTamperedSequenceHeaderDecryptReturnsFalse(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] plaintext = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(
            s_key32, s_plaintextShort, envelope, nonce,
            s_aad, seq: 0x01020304u, algorithm, out int written);

        envelope[8] ^= 0xFF;

        _ = Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
            AeadEngine.Decrypt(s_key32, envelope.AsSpan()[..written], plaintext, s_aad, out _));
    }

    [Fact]
    public void AeadEngineEnvelopeEmptyEnvelopeDecryptReturnsFalse()
    {
        byte[] ptBuf = new byte[10];
        _ = Assert.Throws<CipherException>(() => AeadEngine.Decrypt(s_key32, [], ptBuf, s_aad, out _));
    }

    [Fact]
    public void AeadEngineEnvelopeTruncatedToHeaderOnlyDecryptReturnsFalse()
    {
        int nLen = ChaCha20NonceLen;
        byte[] envelope = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] ptBuf = new byte[s_plaintextShort.Length];

        AeadEngine.Encrypt(s_key32, s_plaintextShort, envelope, s_nonce12,
            s_aad, seq: 2u, CipherSuiteType.Chacha20Poly1305, out _);

        _ = Assert.Throws<CipherException>(() => AeadEngine.Decrypt(
            s_key32, envelope.AsSpan()[..HeaderSize], ptBuf, s_aad, out _));
    }

    [Fact]
    public void AeadEngineChaCha20Poly1305AndSalsa20Poly1305ProduceDifferentOutput()
    {
        byte[] ct1 = new byte[s_plaintextShort.Length];
        byte[] tag1 = new byte[TagSize];
        byte[] ct2 = new byte[s_plaintextShort.Length];
        byte[] tag2 = new byte[TagSize];

        _ = ChaCha20Poly1305.Encrypt(s_key32, s_nonce12, s_plaintextShort, s_aad, ct1, tag1);
        _ = Salsa20Poly1305.Encrypt(s_key32, s_nonce8, s_plaintextShort, s_aad, ct2, tag2);

        Assert.NotEqual(ct1, ct2);
        Assert.NotEqual(tag1, tag2);
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void AeadEngineDifferentKeysProduceDifferentCiphertext(CipherSuiteType algorithm)
    {
        byte[] nonce = NonceFor(algorithm);
        int nLen = NonceLen(algorithm);
        int ctStart = HeaderSize + nLen;

        byte[] env1 = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] env2 = new byte[EnvelopeSize(nLen, s_plaintextShort.Length)];
        byte[] key2 = new byte[32]; key2[0] = 0xAB;

        AeadEngine.Encrypt(s_key32, s_plaintextShort, env1, nonce, s_aad, seq: 1u, algorithm, out int w1);
        AeadEngine.Encrypt(key2, s_plaintextShort, env2, nonce, s_aad, seq: 1u, algorithm, out int w2);

        byte[] ct1 = env1[ctStart..w1];
        byte[] ct2 = env2[ctStart..w2];

        Assert.NotEqual(ct1, ct2);
    }
}
