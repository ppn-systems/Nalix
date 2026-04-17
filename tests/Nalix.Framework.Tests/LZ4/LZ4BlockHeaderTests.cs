// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using Nalix.Framework.LZ4;
using Xunit;

namespace Nalix.Framework.Tests.LZ4;

public sealed class LZ4BlockHeaderTests
{
    [Fact]
    public void SizeConstantMatchesMarshalAndUnsafeSize()
    {
        Assert.Equal(8, LZ4BlockHeader.Size);
        Assert.Equal(LZ4BlockHeader.Size, Marshal.SizeOf<LZ4BlockHeader>());
        Assert.Equal(8, Marshal.SizeOf<LZ4BlockHeader>());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 9)]
    [InlineData(123, 64)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(-1, -2)]
    public void ConstructorStoresOriginalAndCompressedLengths(int originalLength, int compressedLength)
    {
        LZ4BlockHeader header = new(originalLength, compressedLength);

        Assert.Equal(originalLength, header.OriginalLength);
        Assert.Equal(compressedLength, header.CompressedLength);
    }
}
