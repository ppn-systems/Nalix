// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Codec.Security.Hashing;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// Unit tests for HmacKeccak256. This is a Nalix-specific HMAC construction over the
/// Ethereum-style Keccak-256 primitive (RFC 2104 HMAC structure, non-standard hash).
/// No external published test vectors exist for HMAC-over-Ethereum-Keccak256, so these
/// are regression known-answer tests pinning the current implementation's exact output.
/// </summary>
public sealed class HmacKeccak256Tests
{
    private static byte[] HexToBytes(string hex)
    {
        byte[] result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return result;
    }

    /// <summary>
    /// Regression KAT: short key (&lt; 136-byte block size), short data.
    /// </summary>
    [Fact]
    public void ComputeMatchesRegressionKnownAnswerVectorForShortKey()
    {
        byte[] key = System.Text.Encoding.ASCII.GetBytes("nalix-hmac-key");
        byte[] data = System.Text.Encoding.ASCII.GetBytes("Nalix HmacKeccak256 regression payload");
        byte[] expected = HexToBytes("AB6D821261C9E150445DE17FED36ECACE604D2F35F008E1982DC7D9CD612972D");

        byte[] output = new byte[32];
        HmacKeccak256.Compute(key, data, output);

        Assert.Equal(expected, output);
    }

    /// <summary>
    /// Regression KAT: key longer than the 136-byte block size, forcing the
    /// key-hashing branch (<c>Keccak256.HashData(key)</c>) inside <see cref="HmacKeccak256.Compute"/>.
    /// </summary>
    [Fact]
    public void ComputeMatchesRegressionKnownAnswerVectorForLongKey()
    {
        byte[] key = new byte[200];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)i;
        }

        byte[] data = System.Text.Encoding.ASCII.GetBytes("Nalix HmacKeccak256 long-key regression payload");
        byte[] expected = HexToBytes("113D687ECF3E96C89D30ED99D801F0C557FA7860D8BECDDC971421C5624F2FB7");

        byte[] output = new byte[32];
        HmacKeccak256.Compute(key, data, output);

        Assert.Equal(expected, output);
    }

    [Fact]
    public void ComputeIsDeterministicForSameInput()
    {
        byte[] key = System.Text.Encoding.ASCII.GetBytes("determinism-key");
        byte[] data = System.Text.Encoding.ASCII.GetBytes("determinism-data");

        byte[] out1 = new byte[32];
        byte[] out2 = new byte[32];
        HmacKeccak256.Compute(key, data, out1);
        HmacKeccak256.Compute(key, data, out2);

        Assert.Equal(out1, out2);
        Assert.NotEqual(new byte[32], out1);
    }

    [Fact]
    public void ComputeWithDifferentKeysProducesDifferentOutput()
    {
        byte[] data = System.Text.Encoding.ASCII.GetBytes("same-data");
        byte[] key1 = System.Text.Encoding.ASCII.GetBytes("key-one");
        byte[] key2 = System.Text.Encoding.ASCII.GetBytes("key-two");

        byte[] out1 = new byte[32];
        byte[] out2 = new byte[32];
        HmacKeccak256.Compute(key1, data, out1);
        HmacKeccak256.Compute(key2, data, out2);

        Assert.NotEqual(out1, out2);
    }

    [Fact]
    public void ComputeWhenOutputTooSmallThrowsArgumentException()
    {
        byte[] key = [1, 2, 3];
        byte[] data = [4, 5, 6];
        byte[] tooSmall = new byte[31];

        _ = Assert.Throws<ArgumentException>(() => HmacKeccak256.Compute(key, data, tooSmall));
    }
}
