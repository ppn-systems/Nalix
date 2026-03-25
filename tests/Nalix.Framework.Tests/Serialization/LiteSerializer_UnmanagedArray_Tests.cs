// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Linq;
using Nalix.Common.Exceptions;
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public class LiteSerializerUnmanagedArrayTests
{
    [Fact]
    public void SerializeUnmanagedArrayNullReturnsNullMarkerIntMinus1()
    {
        SmallStruct[] arr = null;

        byte[] data = LiteSerializer.Serialize(arr);
        Assert.Equal(4, data.Length);
        // Marker null là -1 (0xFFFFFFFF)
        Assert.Equal(-1, BitConverter.ToInt32(data, 0));

        SmallStruct[] back = []; // sẽ bị set về null
        int read = LiteSerializer.Deserialize(data, ref back);
        Assert.Equal(4, read);
        Assert.Null(back);
    }

    [Fact]
    public void SerializeUnmanagedArrayEmptyReturnsZeroMarker()
    {
        SmallStruct[] arr = [];

        byte[] data = LiteSerializer.Serialize(arr);
        Assert.Equal(4, data.Length);
        Assert.Equal(0, BitConverter.ToInt32(data, 0));

        SmallStruct[] back = null;
        int read = LiteSerializer.Deserialize(data, ref back);
        Assert.Equal(4, read);
        Assert.NotNull(back);
        Assert.Empty(back);
    }

    [Theory]
    [InlineData(new byte[] { 1 })]
    [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
    public void SerializeDeserializeUnmanagedArrayByteArrayRoundTrip(byte[] payload)
    {
        // Dùng byte[] để test nhanh nhánh UnmanagedSZArray
        byte[] data = LiteSerializer.Serialize(payload);

        byte[] back = null;
        int read = LiteSerializer.Deserialize(data, ref back);

        Assert.Equal(data.Length, read);
        Assert.NotNull(back);
        Assert.True(payload.SequenceEqual(back));
    }

    [Fact]
    public void SerializeDeserializeUnmanagedArrayStructArrayRoundTrip()
    {
        SmallStruct[] arr = [.. Enumerable.Range(1, 100).Select(i => new SmallStruct { A = (byte)(i % 256) })];

        byte[] data = LiteSerializer.Serialize(arr);

        SmallStruct[] back = null;
        int read = LiteSerializer.Deserialize(data, ref back);

        Assert.Equal(data.Length, read);
        Assert.NotNull(back);
        Assert.Equal(arr.Length, back.Length);
        for (int i = 0; i < arr.Length; i++)
        {
            Assert.Equal(arr[i].A, back[i].A);
        }
    }

    [Fact]
    public void DeserializeUnmanagedArrayBufferTooShortForLengthThrows()
    {
        // Ít hơn 4 byte => không đủ length prefix
        byte[] bad = new byte[3];
        SmallStruct[] dest = null;
        _ = Assert.Throws<SerializationException>(() => LiteSerializer.Deserialize(bad, ref dest));
    }

    [Fact]
    public void DeserializeUnmanagedArrayLengthDeclaredButDataInsufficientThrows()
    {
        // length = 5 nhưng chỉ có 4 bytes data (mỗi phần tử 1 byte)
        byte[] buf = new byte[4 + 4];
        BitConverter.GetBytes(5).CopyTo(buf, 0); // prefix length = 5
        // chỉ ghi 4 byte data
        for (int i = 0; i < 4; i++)
        {
            buf[4 + i] = (byte)(i + 1);
        }

        SmallStruct[] dest = null;
        _ = Assert.Throws<SerializationException>(() => LiteSerializer.Deserialize(buf, ref dest));
    }
}