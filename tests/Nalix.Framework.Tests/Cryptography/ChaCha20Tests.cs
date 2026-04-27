// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Exceptions;
using Nalix.Codec.Security.Symmetric;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class ChaCha20Tests
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

    [Fact]
    public void EncryptThenDecryptAcrossMultipleBlocksRoundTrips()
    {
        byte[] key = SequentialBytes(ChaCha20.KeySize, 1);
        byte[] nonce = SequentialBytes(ChaCha20.NonceSize, 50);
        byte[] plaintext = SequentialBytes(ChaCha20.BlockSize * 2 + 17, 100);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] decrypted = new byte[plaintext.Length];

        ChaCha20 encryptor = new(key, nonce, 7u);
        int written = encryptor.Encrypt(plaintext, ciphertext);
        encryptor.Clear();

        ChaCha20 decryptor = new(key, nonce, 7u);
        int recovered = decryptor.Decrypt(ciphertext, decrypted);
        decryptor.Clear();

        Assert.Equal(plaintext.Length, written);
        Assert.Equal(ciphertext.Length, recovered);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void ConstructorWithInvalidKeyLengthThrowsCipherException()
    {
        byte[] invalidKey = new byte[ChaCha20.KeySize - 1];
        byte[] nonce = new byte[ChaCha20.NonceSize];

        _ = Assert.ThrowsAny<CipherException>(() => new ChaCha20(invalidKey, nonce, 0u));
    }

    [Fact]
    public void ConstructorWithInvalidNonceLengthThrowsCipherException()
    {
        byte[] key = new byte[ChaCha20.KeySize];
        byte[] invalidNonce = new byte[ChaCha20.NonceSize - 1];

        _ = Assert.ThrowsAny<CipherException>(() => new ChaCha20(key, invalidNonce, 0u));
    }

    [Fact]
    public void EncryptWhenDestinationIsTooSmallThrowsCipherException()
    {
        byte[] key = SequentialBytes(ChaCha20.KeySize);
        byte[] nonce = SequentialBytes(ChaCha20.NonceSize);
        byte[] plaintext = SequentialBytes(10);
        byte[] destination = new byte[plaintext.Length - 1];

        ChaCha20 cipher = new(key, nonce, 0u);

        _ = Assert.ThrowsAny<CipherException>(() => cipher.Encrypt(plaintext, destination));

        cipher.Clear();
    }

    [Fact]
    public void OperationsAfterClearThrowObjectDisposedException()
    {
        byte[] key = SequentialBytes(ChaCha20.KeySize);
        byte[] nonce = SequentialBytes(ChaCha20.NonceSize);
        byte[] input = SequentialBytes(8);
        byte[] output = new byte[input.Length];
        byte[] block = new byte[ChaCha20.BlockSize];

        ChaCha20 cipher = new(key, nonce, 0u);
        cipher.Clear();

        _ = Assert.Throws<ObjectDisposedException>(() => cipher.GenerateKeyBlock(block));
        _ = Assert.Throws<ObjectDisposedException>(() => cipher.Encrypt(input, output));
        _ = Assert.Throws<ObjectDisposedException>(() => cipher.Decrypt(input, output));
    }

    [Fact]
    public void GenerateKeyBlockAtCounterMaxValueThrowsCipherException()
    {
        byte[] key = SequentialBytes(ChaCha20.KeySize);
        byte[] nonce = SequentialBytes(ChaCha20.NonceSize);
        byte[] block = new byte[ChaCha20.BlockSize];

        ChaCha20 cipher = new(key, nonce, uint.MaxValue);

        _ = Assert.ThrowsAny<CipherException>(() => cipher.GenerateKeyBlock(block));
    }
}













