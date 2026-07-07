// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Threading.Tasks;
using Nalix.Abstractions.Security;
using Nalix.Codec.Security;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// Concurrency stress tests for the layers documented as stateless/thread-safe:
/// <see cref="EnvelopeCipher"/> wraps <c>AeadEngine</c>/<c>SymmetricEngine</c>, both stateless
/// static classes per <see cref="EnvelopeCipher"/>'s own XML doc ("stateless and safe for
/// concurrent use") — a single shared key is exercised from many threads with no per-thread
/// instance isolation, since none is needed for stateless static APIs.
/// </summary>
[Trait("Category", "Stress")]
public sealed class ConcurrencyStressTests
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
    [InlineData(CipherSuiteType.Chacha20Poly1305)]
    [InlineData(CipherSuiteType.Salsa20Poly1305)]
    public void EnvelopeCipherHandles10kConcurrentRoundTripsAcrossEightThreads(CipherSuiteType suite)
    {
        const int threadCount = 8;
        const int opsPerThread = 1250; // 8 * 1250 = 10,000
        byte[] key = SequentialBytes(32, 1);

        int size = EnvelopeCipher.HeaderSize + EnvelopeCipher.GetNonceLength(suite)
                   + 32 + EnvelopeCipher.GetTagLength(suite);

        Parallel.For(0, threadCount, threadIndex =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                byte[] plaintext = SequentialBytes(32, threadIndex * 1000 + i);
                byte[] envelope = new byte[size];
                byte[] decrypted = new byte[32];

                EnvelopeCipher.Encrypt(key, plaintext, envelope, seq: (uint)(threadIndex * opsPerThread + i), suite, out int written);
                EnvelopeCipher.Decrypt(key, new ReadOnlySpan<byte>(envelope, 0, written), decrypted, suite, out int decWritten, out _);

                Assert.Equal(plaintext.Length, decWritten);
                Assert.True(plaintext.SequenceEqual(decrypted),
                    $"thread={threadIndex} op={i}: round-trip mismatch under concurrent EnvelopeCipher use.");
            }
        });
    }

    /// <summary>
    /// <see cref="HandshakeX25519"/>'s public methods are pure functions with no shared mutable
    /// state; verify parallel handshake derivations from independent key material never produce
    /// torn/cross-contaminated results.
    /// </summary>
    [Fact]
    public void HandshakeX25519DerivationsAreIsolatedUnderConcurrentLoad()
    {
        const int threadCount = 8;
        const int opsPerThread = 500; // 8 * 500 = 4,000

        Parallel.For(0, threadCount, threadIndex =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                byte[] raw = new byte[32];
                for (int b = 0; b < 32; b++)
                {
                    raw[b] = (byte)(threadIndex * 31 + i + b);
                }

                Nalix.Abstractions.Primitives.Bytes32 secret = new(raw);
                Nalix.Abstractions.Primitives.Bytes32 expected = HandshakeX25519.DeriveRekeySecret(secret);
                Nalix.Abstractions.Primitives.Bytes32 actual = HandshakeX25519.DeriveRekeySecret(secret);

                Assert.True(expected.AsSpan().SequenceEqual(actual.AsSpan()),
                    $"thread={threadIndex} op={i}: HandshakeX25519.DeriveRekeySecret produced inconsistent output under concurrent load.");
            }
        });
    }
}
