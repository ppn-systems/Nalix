// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;
using Nalix.Codec.Security.Hashing;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class Poly1305Tests
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

    [Fact]
    public void ComputeIsDeterministicForSameInput()
    {
        byte[] key = HexToBytes("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b");
        byte[] message = Encoding.ASCII.GetBytes("Cryptographic Forum Research Group");
        byte[] tag1 = Poly1305.Compute(key, message);
        byte[] tag2 = Poly1305.Compute(key, message);

        Assert.Equal(Poly1305.TagSize, tag1.Length);
        Assert.Equal(tag1, tag2);
        Assert.NotEqual(new byte[Poly1305.TagSize], tag1);
    }

    [Fact]
    public void IncrementalUpdateAndFinalizeMatchOneShotCompute()
    {
        byte[] key = HexToBytes("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b");
        byte[] message = Encoding.ASCII.GetBytes("Cryptographic Forum Research Group");

        byte[] oneShot = Poly1305.Compute(key, message);
        byte[] incremental = new byte[Poly1305.TagSize];

        Poly1305 poly = new(key);
        try
        {
            poly.Update(message.AsSpan(0, 10));
            poly.Update(message.AsSpan(10, 7));
            poly.Update(message.AsSpan(17));
            poly.FinalizeTag(incremental);
        }
        finally
        {
            poly.Clear();
        }

        Assert.Equal(oneShot, incremental);
    }

    [Fact]
    public void VerifyReturnsTrueForValidTagAndFalseForTamperedTag()
    {
        byte[] key = HexToBytes("85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b");
        byte[] message = Encoding.ASCII.GetBytes("Cryptographic Forum Research Group");
        byte[] tag = Poly1305.Compute(key, message);

        Assert.True(Poly1305.Verify(key, message, tag));

        tag[0] ^= 0xFF;
        Assert.False(Poly1305.Verify(key, message, tag));
    }

    [Fact]
    public void VerifyWhenTagLengthInvalidThrowsArgumentException()
    {
        byte[] key = new byte[Poly1305.KeySize];
        byte[] message = [1, 2, 3];
        byte[] invalidTag = new byte[Poly1305.TagSize - 1];

        _ = Assert.Throws<ArgumentException>(() => Poly1305.Verify(key, message, invalidTag));
    }

    [Fact]
    public void ComputeWhenKeyOrDestinationInvalidThrowsArgumentException()
    {
        byte[] invalidKey = new byte[Poly1305.KeySize - 1];
        byte[] message = [1, 2, 3];
        byte[] tooSmallDestination = new byte[Poly1305.TagSize - 1];

        _ = Assert.Throws<ArgumentException>(() => Poly1305.Compute(invalidKey, message, new byte[Poly1305.TagSize]));
        _ = Assert.Throws<ArgumentException>(() => Poly1305.Compute(new byte[Poly1305.KeySize], message, tooSmallDestination));
    }

    [Fact]
    public void FinalizeTwiceOrUpdateAfterFinalizeThrowsInvalidOperationException()
    {
        byte[] key = new byte[Poly1305.KeySize];
        byte[] output = new byte[Poly1305.TagSize];

        Poly1305 poly = new(key);
        try
        {
            poly.Update([1, 2, 3]);
            poly.FinalizeTag(output);

            _ = Assert.Throws<InvalidOperationException>(() => poly.FinalizeTag(output));
            _ = Assert.Throws<InvalidOperationException>(() => poly.Update([4, 5]));
        }
        finally
        {
            poly.Clear();
        }
    }

    [Fact]
    public void OperationsAfterClearThrowObjectDisposedException()
    {
        byte[] key = new byte[Poly1305.KeySize];
        byte[] output = new byte[Poly1305.TagSize];

        Poly1305 poly = new(key);
        poly.Clear();

        _ = Assert.Throws<ObjectDisposedException>(() => poly.ComputeTag([1, 2], output));
        _ = Assert.Throws<ObjectDisposedException>(() => poly.Update([1, 2]));
        _ = Assert.Throws<ObjectDisposedException>(() => poly.FinalizeTag(output));
    }
}













