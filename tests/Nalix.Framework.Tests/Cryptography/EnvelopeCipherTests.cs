// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Exceptions;
using Nalix.Common.Security;
using Nalix.Framework.Security;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class EnvelopeCipherTests
{
    private static byte[] SequentialBytes(int length, int start = 0)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(start + i);
        }

        return data;
    }

    private static int EnvelopeSize(CipherSuiteType suite, int plaintextLength)
        => EnvelopeCipher.HeaderSize
           + EnvelopeCipher.GetNonceLength(suite)
           + plaintextLength
           + EnvelopeCipher.GetTagLength(suite);

    [Theory]
    [InlineData(CipherSuiteType.Chacha20, 12, 0)]
    [InlineData(CipherSuiteType.Salsa20, 8, 0)]
    [InlineData(CipherSuiteType.Chacha20Poly1305, 12, 16)]
    [InlineData(CipherSuiteType.Salsa20Poly1305, 8, 16)]
    public void GetNonceLengthAndTagLengthReturnExpectedValues(CipherSuiteType suite, int expectedNonceLength, int expectedTagLength)
    {
        Assert.Equal(expectedNonceLength, EnvelopeCipher.GetNonceLength(suite));
        Assert.Equal(expectedTagLength, EnvelopeCipher.GetTagLength(suite));
    }

    [Fact]
    public void GetNonceLengthAndTagLengthWithUnsupportedSuiteThrowCipherException()
    {
        CipherSuiteType invalid = (CipherSuiteType)255;

        _ = Assert.Throws<CipherException>(() => EnvelopeCipher.GetNonceLength(invalid));
        _ = Assert.Throws<CipherException>(() => EnvelopeCipher.GetTagLength(invalid));
    }

    [Theory]
    [InlineData(CipherSuiteType.Chacha20)]
    [InlineData(CipherSuiteType.Salsa20)]
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void EncryptThenDecryptRoundTripsForAllSupportedSuites(CipherSuiteType suite)
    {
        byte[] key = SequentialBytes(32, 1);
        byte[] plaintext = SequentialBytes(137, 20);
        byte[] aad = SequentialBytes(11, 80);
        byte[] envelope = new byte[EnvelopeSize(suite, plaintext.Length)];
        byte[] decrypted = new byte[plaintext.Length];

        EnvelopeCipher.Encrypt(key, plaintext, envelope, aad, seq: 77u, suite, out int encWritten);
        EnvelopeCipher.Decrypt(key, new System.ReadOnlySpan<byte>(envelope, 0, encWritten), decrypted, aad, suite, out int decWritten);

        Assert.Equal(envelope.Length, encWritten);
        Assert.Equal(plaintext.Length, decWritten);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void DecryptWhenExpectedAlgorithmDoesNotMatchEnvelopeThrowsCipherException()
    {
        byte[] key = SequentialBytes(32, 1);
        byte[] plaintext = SequentialBytes(32, 2);
        byte[] envelope = new byte[EnvelopeSize(CipherSuiteType.Chacha20, plaintext.Length)];
        byte[] decrypted = new byte[plaintext.Length];

        EnvelopeCipher.Encrypt(key, plaintext, envelope, seq: 1u, CipherSuiteType.Chacha20, out _);

        _ = Assert.Throws<CipherException>(() =>
            EnvelopeCipher.Decrypt(key, envelope, decrypted, CipherSuiteType.Salsa20, out _));
    }

    [Fact]
    public void EncryptWhenOutputBufferIsTooSmallThrowsArgumentException()
    {
        byte[] key = SequentialBytes(32, 1);
        byte[] plaintext = SequentialBytes(64, 7);
        byte[] tooSmall = new byte[EnvelopeSize(CipherSuiteType.Chacha20Poly1305, plaintext.Length) - 1];

        _ = Assert.Throws<ArgumentException>(() =>
            EnvelopeCipher.Encrypt(key, plaintext, tooSmall, seq: null, CipherSuiteType.Chacha20Poly1305, out _));
    }
}
