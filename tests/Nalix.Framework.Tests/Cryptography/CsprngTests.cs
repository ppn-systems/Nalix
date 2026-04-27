// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Environment.Random;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class CsprngTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-128)]
    [InlineData(Csprng.MaxByteArrayLength + 1)]
    [InlineData(int.MaxValue)]
    public void GetBytesWhenLengthIsOutOfRangeThrowsArgumentOutOfRangeException(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Csprng.GetBytes(length));
    }

    [Fact]
    public void GetBytesWhenLengthIsZeroReturnsEmptyArray()
    {
        byte[] bytes = Csprng.GetBytes(0);

        Assert.Empty(bytes);
    }

    [Fact]
    public void CreateNonceWhenCalledWithDefaultLengthReturnsTwelveBytes()
    {
        byte[] nonce = Csprng.CreateNonce();

        Assert.Equal(12, nonce.Length);
    }

    [Fact]
    public void CreateNonceWhenLengthIsCustomReturnsExpectedLength()
    {
        byte[] nonce = Csprng.CreateNonce(24);

        Assert.Equal(24, nonce.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Csprng.MaxByteArrayLength + 1)]
    [InlineData(int.MaxValue)]
    public void CreateNonceWhenLengthIsOutOfRangeThrowsArgumentOutOfRangeException(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Csprng.CreateNonce(length));
    }

    [Fact]
    public void NextBytesWhenBufferIsNullThrowsArgumentNullException()
    {
        byte[]? buffer = null;

        Assert.Throws<ArgumentNullException>(() => Csprng.NextBytes(buffer!));
    }

    [Fact]
    public void GetInt32WhenRangeIsInvalidThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Csprng.GetInt32(10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Csprng.GetInt32(10, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => Csprng.GetInt32(0));
    }

    [Fact]
    public void GetInt32WhenRangeIsValidAlwaysReturnsValuesInsideBounds()
    {
        const int min = -50;
        const int max = 75;

        for (int i = 0; i < 256; i++)
        {
            int value = Csprng.GetInt32(min, max);
            Assert.InRange(value, min, max - 1);
        }
    }

    [Fact]
    public void NextDoubleAlwaysReturnsValueInHalfOpenUnitInterval()
    {
        for (int i = 0; i < 128; i++)
        {
            double value = Csprng.NextDouble();
            Assert.InRange(value, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void FillWhenSpanIsEmptyDoesNotThrow()
    {
        Span<byte> empty = [];

        Csprng.Fill(empty);
    }
}













