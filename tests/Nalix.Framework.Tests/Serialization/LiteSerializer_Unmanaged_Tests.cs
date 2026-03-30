// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Common.Exceptions;
using Nalix.Framework.Serialization;
using Xunit;

namespace Nalix.Framework.Tests.Serialization;

public class LiteSerializerUnmanagedTests
{
    [Fact]
    public void SerializeDeserializeUnmanagedStructRoundTrip()
    {
        ComplexStruct input = new() { I32 = 123456789, I16 = -1234, B = 0xAB };

        // byte[] path (unmanaged -> không cần formatter)
        byte[] data = LiteSerializer.Serialize(in input);

        ComplexStruct output = default;
        int read = LiteSerializer.Deserialize(data, ref output);

        Assert.Equal(data.Length, read);
        Assert.Equal(input.I32, output.I32);
        Assert.Equal(input.I16, output.I16);
        Assert.Equal(input.B, output.B);
    }

    [Fact]
    public void SerializeDeserializeNullClassRoundTrip()
    {
        NullClass input = new() { I32 = null, I16 = null };

        // byte[] path (unmanaged -> không cần formatter)
        byte[] data = LiteSerializer.Serialize(in input);

        NullClass output = default;
        int read = LiteSerializer.Deserialize(data, ref output);

        Assert.Equal(data.Length, read);
        Assert.Equal(input.I32, output.I32);
        Assert.Equal(input.I16, output.I16);
    }

    [Fact]
    public void SerializeUnmanagedStructToProvidedBufferExactSizeOk()
    {
        ComplexStruct value = new() { I32 = 42, I16 = 7, B = 1 };
        // Lấy size từ serialize để có kích cỡ tối thiểu
        byte[] tmp = LiteSerializer.Serialize(in value);
        byte[] buffer = new byte[tmp.Length];

        int written = LiteSerializer.Serialize(in value, buffer);
        Assert.Equal(tmp.Length, written);

        ComplexStruct back = default;
        int read = LiteSerializer.Deserialize(buffer, ref back);

        Assert.Equal(buffer.Length, read);
        Assert.Equal(value.I32, back.I32);
        Assert.Equal(value.I16, back.I16);
        Assert.Equal(value.B, back.B);
    }

    [Fact]
    public void SerializeUnmanagedStructBufferTooSmallThrows()
    {
        ComplexStruct value = new() { I32 = 1, I16 = 2, B = 3 };
        // Lấy mảng đúng size rồi cắt nhỏ đi 1 byte
        byte[] exact = LiteSerializer.Serialize(in value);
        byte[] tooSmall = new byte[exact.Length - 1];

        _ = Assert.Throws<SerializationFailureException>(() => LiteSerializer.Serialize(in value, tooSmall));
    }

    [Fact]
    public void DeserializeUnmanagedStructBufferTooSmallThrows()
    {
        ComplexStruct value = new() { I32 = 1, I16 = 2, B = 3 };
        byte[] full = LiteSerializer.Serialize(in value);
        byte[] small = new byte[full.Length - 1];

        // copy thiếu dữ liệu
        Array.Copy(full, small, small.Length);

        ComplexStruct dest = default;
        _ = Assert.Throws<SerializationFailureException>(() => LiteSerializer.Deserialize(small, ref dest));
    }
}
