using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Serialization;
public class LiteSerializer_Unmanaged_Tests
{
    [Fact]
    public void SerializeDeserialize_UnmanagedStruct_RoundTrip()
    {
        var input = new ComplexStruct { I32 = 123456789, I16 = -1234, B = 0xAB };

        // byte[] path (unmanaged -> không cần formatter)
        Byte[] data = LiteSerializer.Serialize(in input);

        var output = default(ComplexStruct);
        Int32 read = LiteSerializer.Deserialize<ComplexStruct>(data, ref output);

        Assert.Equal(data.Length, read);
        Assert.Equal(input.I32, output.I32);
        Assert.Equal(input.I16, output.I16);
        Assert.Equal(input.B, output.B);
    }

    [Fact]
    public void Serialize_UnmanagedStruct_ToProvidedBuffer_ExactSize_Ok()
    {
        var value = new ComplexStruct { I32 = 42, I16 = 7, B = 1 };
        // Lấy size từ serialize để có kích cỡ tối thiểu
        Byte[] tmp = LiteSerializer.Serialize(in value);
        var buffer = new Byte[tmp.Length];

        Int32 written = LiteSerializer.Serialize(in value, buffer);
        Assert.Equal(tmp.Length, written);

        var back = default(ComplexStruct);
        Int32 read = LiteSerializer.Deserialize<ComplexStruct>(buffer, ref back);

        Assert.Equal(buffer.Length, read);
        Assert.Equal(value.I32, back.I32);
        Assert.Equal(value.I16, back.I16);
        Assert.Equal(value.B, back.B);
    }

    [Fact]
    public void Serialize_UnmanagedStruct_BufferTooSmall_Throws()
    {
        var value = new ComplexStruct { I32 = 1, I16 = 2, B = 3 };
        // Lấy mảng đúng size rồi cắt nhỏ đi 1 byte
        Byte[] exact = LiteSerializer.Serialize(in value);
        Byte[] tooSmall = new Byte[exact.Length - 1];

        _ = Assert.Throws<SerializationException>(() => LiteSerializer.Serialize(in value, tooSmall));
    }

    [Fact]
    public void Deserialize_UnmanagedStruct_BufferTooSmall_Throws()
    {
        var value = new ComplexStruct { I32 = 1, I16 = 2, B = 3 };
        Byte[] full = LiteSerializer.Serialize(in value);
        var small = new Byte[full.Length - 1];

        // copy thiếu dữ liệu
        Array.Copy(full, small, small.Length);

        var dest = default(ComplexStruct);
        _ = Assert.Throws<SerializationException>(() => LiteSerializer.Deserialize<ComplexStruct>(small, ref dest));
    }

    [Fact]
    public void Serialize_ToSpan_UnmanagedStruct_NotSupported()
    {
        var value = new ComplexStruct { I32 = 1, I16 = 2, B = 3 };
        // Move span declaration inside the lambda to avoid CS8175
        _ = Assert.Throws<NotSupportedException>(() =>
        {
            Span<Byte> span = stackalloc Byte[64];
            _ = LiteSerializer.Serialize(in value, span);
        });
    }
}
