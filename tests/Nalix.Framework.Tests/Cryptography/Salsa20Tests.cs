// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Framework.Security.Symmetric;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class Salsa20Tests
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

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void EncryptThenDecryptRoundTripsForSupportedKeyLengths(int keyLength)
    {
        byte[] key = SequentialBytes(keyLength, 1);
        byte[] nonce = SequentialBytes(Salsa20.NonceSize, 50);
        byte[] plaintext = SequentialBytes(170, 100);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] recovered = new byte[plaintext.Length];

        int encrypted = Salsa20.Encrypt(key, nonce, 3UL, plaintext, ciphertext);
        int decrypted = Salsa20.Decrypt(key, nonce, 3UL, ciphertext, recovered);

        Assert.Equal(plaintext.Length, encrypted);
        Assert.Equal(ciphertext.Length, decrypted);
        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void EncryptWithSameInputsProducesDeterministicOutput()
    {
        byte[] key = SequentialBytes(32, 1);
        byte[] nonce = SequentialBytes(Salsa20.NonceSize, 3);
        byte[] plaintext = SequentialBytes(128, 11);

        byte[] c1 = new byte[plaintext.Length];
        byte[] c2 = new byte[plaintext.Length];

        _ = Salsa20.Encrypt(key, nonce, 99UL, plaintext, c1);
        _ = Salsa20.Encrypt(key, nonce, 99UL, plaintext, c2);

        Assert.Equal(c1, c2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(31)]
    public void EncryptWithInvalidKeyLengthThrowsArgumentException(int keyLength)
    {
        byte[] key = new byte[keyLength];
        byte[] nonce = new byte[Salsa20.NonceSize];
        byte[] plaintext = new byte[8];
        byte[] ciphertext = new byte[8];

        _ = Assert.Throws<ArgumentException>(() => Salsa20.Encrypt(key, nonce, 0UL, plaintext, ciphertext));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void EncryptWithInvalidNonceLengthThrowsArgumentException(int nonceLength)
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[nonceLength];
        byte[] plaintext = new byte[8];
        byte[] ciphertext = new byte[8];

        _ = Assert.Throws<ArgumentException>(() => Salsa20.Encrypt(key, nonce, 0UL, plaintext, ciphertext));
    }

    [Fact]
    public void DecryptWhenDestinationTooSmallThrowsArgumentException()
    {
        byte[] key = SequentialBytes(32);
        byte[] nonce = SequentialBytes(Salsa20.NonceSize);
        byte[] ciphertext = SequentialBytes(32, 10);
        byte[] tooSmall = new byte[ciphertext.Length - 1];

        _ = Assert.Throws<ArgumentException>(() => Salsa20.Decrypt(key, nonce, 0UL, ciphertext, tooSmall));
    }
}
