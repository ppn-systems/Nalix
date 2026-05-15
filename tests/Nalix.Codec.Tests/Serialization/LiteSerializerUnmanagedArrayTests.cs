// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Linq;
using Nalix.Abstractions.Exceptions;
using Nalix.Codec.Serialization;
using Xunit;

namespace Nalix.Codec.Tests.Serialization;

public sealed class LiteSerializerUnmanagedArrayTests
{
    [Fact]
    public void Serialize_UnmanagedArrayNull_WritesMinusOneMarker()
    {
        SmallStruct[]? input = null;

        byte[] data = LiteSerializer.Serialize(input);
        SmallStruct[]? output = [];
        int read = LiteSerializer.Deserialize(data, ref output);

        Assert.Equal(4, data.Length);
        Assert.Equal(-1, BitConverter.ToInt32(data, 0));
        Assert.Equal(4, read);
        Assert.Null(output);
    }

    [Fact]
    public void Serialize_UnmanagedArrayEmpty_WritesZeroMarker()
    {
        SmallStruct[] input = [];

        byte[] data = LiteSerializer.Serialize(input);
        SmallStruct[]? output = null;
        int read = LiteSerializer.Deserialize(data, ref output);

        Assert.Equal(4, data.Length);
        Assert.Equal(0, BitConverter.ToInt32(data, 0));
        Assert.Equal(4, read);
        Assert.NotNull(output);
        Assert.Empty(output);
    }

    [Theory]
    [InlineData(new byte[] { 1 })]
    [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
    public void SerializeDeserialize_UnmanagedByteArray_RoundTripsPayload(byte[] payload)
    {
        byte[] data = LiteSerializer.Serialize(payload);
        byte[]? output = null;
        int read = LiteSerializer.Deserialize(data, ref output);

        Assert.Equal(data.Length, read);
        Assert.NotNull(output);
        Assert.True(payload.SequenceEqual(output));
    }

    [Fact]
    public void SerializeDeserialize_UnmanagedStructArray_RoundTripsPayload()
    {
        SmallStruct[] input = [.. Enumerable.Range(1, 100).Select(i => new SmallStruct { A = (byte)(i % 256) })];
        byte[] data = LiteSerializer.Serialize(input);
        SmallStruct[]? output = null;
        int read = LiteSerializer.Deserialize(data, ref output);

        Assert.Equal(data.Length, read);
        Assert.NotNull(output);
        Assert.Equal(input.Length, output.Length);

        for (int index = 0; index < input.Length; index++)
        {
            Assert.Equal(input[index].A, output[index].A);
        }
    }

    [Fact]
    public void Deserialize_UnmanagedArray_BufferTooShortForLength_ThrowsSerializationFailureException()
    {
        byte[] bad = new byte[3];
        SmallStruct[]? destination = null;

        _ = Assert.ThrowsAny<SerializationFailureException>(() => LiteSerializer.Deserialize(bad, ref destination));
    }

    [Fact]
    public void Deserialize_UnmanagedArray_DeclaredLengthExceedsData_ThrowsSerializationFailureException()
    {
        byte[] buffer = new byte[8];
        BitConverter.GetBytes(5).CopyTo(buffer, 0);

        for (int index = 0; index < 4; index++)
        {
            buffer[4 + index] = (byte)(index + 1);
        }

        SmallStruct[]? destination = null;
        _ = Assert.ThrowsAny<SerializationFailureException>(() => LiteSerializer.Deserialize(buffer, ref destination));
    }
}















