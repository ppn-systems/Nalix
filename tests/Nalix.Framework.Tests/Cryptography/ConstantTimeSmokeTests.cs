// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using Nalix.Abstractions.Security;
using Nalix.Codec.Security;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// Best-effort statistical smoke test for tag-verification timing — NOT a proof of constant-time
/// behavior (JIT warmup, GC, OS scheduling noise all confound single-machine timing). It only
/// flags a gross (&gt;2x median) discrepancy between a tag mismatch in the first byte vs. the
/// last byte, which is the failure mode an early-exit (non-fixed-time) comparison would produce.
/// See the report's "Constant-time inspection" section for the static grep-based file:line audit
/// of comparison call sites; this test only supplements that with a coarse dynamic check.
/// </summary>
public sealed class ConstantTimeSmokeTests
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
    public void EnvelopeDecryptTagMismatchTimingIsNotGrosslyPositionDependent()
    {
        const int iterations = 100_000;
        const CipherSuiteType suite = CipherSuiteType.Chacha20Poly1305;

        byte[] key = SequentialBytes(32, 1);
        byte[] plaintext = SequentialBytes(32, 2);
        int size = EnvelopeCipher.HeaderSize + EnvelopeCipher.GetNonceLength(suite)
                   + plaintext.Length + EnvelopeCipher.GetTagLength(suite);
        byte[] genuineEnvelope = new byte[size];
        EnvelopeCipher.Encrypt(key, plaintext, genuineEnvelope, seq: 1u, suite, out int written);

        byte[] tamperedFirstByte = (byte[])genuineEnvelope.Clone();
        tamperedFirstByte[^EnvelopeCipher.GetTagLength(suite)] ^= 0xFF;

        byte[] tamperedLastByte = (byte[])genuineEnvelope.Clone();
        tamperedLastByte[^1] ^= 0xFF;

        byte[] decrypted = new byte[plaintext.Length];

        // Warmup (JIT).
        for (int i = 0; i < 1000; i++)
        {
            _ = EnvelopeCipher.TryDecrypt(key, tamperedFirstByte.AsSpan(0, written), decrypted, suite, out _, out _);
            _ = EnvelopeCipher.TryDecrypt(key, tamperedLastByte.AsSpan(0, written), decrypted, suite, out _, out _);
        }

        Stopwatch swFirst = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = EnvelopeCipher.TryDecrypt(key, tamperedFirstByte.AsSpan(0, written), decrypted, suite, out _, out _);
        }
        swFirst.Stop();

        Stopwatch swLast = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = EnvelopeCipher.TryDecrypt(key, tamperedLastByte.AsSpan(0, written), decrypted, suite, out _, out _);
        }
        swLast.Stop();

        double ratio = Math.Max(swFirst.Elapsed.TotalMilliseconds, swLast.Elapsed.TotalMilliseconds)
                       / Math.Max(1, Math.Min(swFirst.Elapsed.TotalMilliseconds, swLast.Elapsed.TotalMilliseconds));

        Assert.True(ratio < 2.0,
            $"Smoke test only, not a timing-attack proof: first-byte-mismatch took {swFirst.ElapsedMilliseconds}ms, " +
            $"last-byte-mismatch took {swLast.ElapsedMilliseconds}ms over {iterations} iterations (ratio {ratio:F2}). " +
            "A >2x gap could indicate an early-exit (non-fixed-time) comparison, but could also be measurement noise.");
    }
}
