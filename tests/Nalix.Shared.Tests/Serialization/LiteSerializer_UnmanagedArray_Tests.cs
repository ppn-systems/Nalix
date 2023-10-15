using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization;
using System;
using System.Linq;
using Xunit;

namespace Nalix.Shared.Tests.Serialization;
public class LiteSerializer_UnmanagedArray_Tests
{
    [Fact]
    public void Serialize_UnmanagedArray_Null_ReturnsNullMarker_IntMinus1()
    {
        SmallStruct[] arr = null;

        Byte[] data = LiteSerializer.Serialize(arr);
        Assert.Equal(4, data.Length);
        // Marker null là -1 (0xFFFFFFFF)
        Assert.Equal(-1, BitConverter.ToInt32(data, 0));

        SmallStruct[] back = []; // sẽ bị set về null
        Int32 read = LiteSerializer.Deserialize<SmallStruct[]>(data, ref back);
        Assert.Equal(4, read);
        Assert.Null(back);
    }

    [Fact]
    public void Serialize_UnmanagedArray_Empty_ReturnsZeroMarker()
    {
        SmallStruct[] arr = [];

        Byte[] data = LiteSerializer.Serialize(arr);
        Assert.Equal(4, data.Length);
        Assert.Equal(0, BitConverter.ToInt32(data, 0));

        SmallStruct[] back = null;
        Int32 read = LiteSerializer.Deserialize<SmallStruct[]>(data, ref back);
        Assert.Equal(4, read);
        Assert.NotNull(back);
        Assert.Empty(back!);
    }

    [Theory]
    [InlineData(new Byte[] { 1 })]
    [InlineData(new Byte[] { 1, 2, 3, 4, 5 })]
    public void SerializeDeserialize_UnmanagedArray_ByteArray_RoundTrip(Byte[] payload)
    {
        // Dùng byte[] để test nhanh nhánh UnmanagedSZArray
        Byte[] data = LiteSerializer.Serialize(payload);

        Byte[] back = null;
        Int32 read = LiteSerializer.Deserialize<Byte[]>(data, ref back);

        Assert.Equal(data.Length, read);
        Assert.NotNull(back);
        Assert.True(payload.SequenceEqual(back!));
    }

    [Fact]
    public void SerializeDeserialize_UnmanagedArray_StructArray_RoundTrip()
    {
        var arr = Enumerable.Range(1, 100).Select(i => new SmallStruct { A = (Byte)(i % 256) }).ToArray();

        Byte[] data = LiteSerializer.Serialize(arr);

        SmallStruct[] back = null;
        Int32 read = LiteSerializer.Deserialize<SmallStruct[]>(data, ref back);

        Assert.Equal(data.Length, read);
        Assert.NotNull(back);
        Assert.Equal(arr.Length, back!.Length);
        for (Int32 i = 0; i < arr.Length; i++)
        {
            Assert.Equal(arr[i].A, back[i].A);
        }
    }

    [Fact]
    public void Deserialize_UnmanagedArray_BufferTooShortForLength_Throws()
    {
        // Ít hơn 4 byte => không đủ length prefix
        var bad = new Byte[3];
        SmallStruct[] dest = null;
        _ = Assert.Throws<SerializationException>(() => LiteSerializer.Deserialize<SmallStruct[]>(bad, ref dest));
    }

    [Fact]
    public void Deserialize_UnmanagedArray_LengthDeclaredButDataInsufficient_Throws()
    {
        // length = 5 nhưng chỉ có 4 bytes data (mỗi phần tử 1 byte)
        Byte[] buf = new Byte[4 + 4];
        BitConverter.GetBytes(5).CopyTo(buf, 0); // prefix length = 5
        // chỉ ghi 4 byte data
        for (Int32 i = 0; i < 4; i++)
        {
            buf[4 + i] = (Byte)(i + 1);
        }

        SmallStruct[] dest = null;
        _ = Assert.Throws<SerializationException>(() => LiteSerializer.Deserialize<SmallStruct[]>(buf, ref dest));
    }
}