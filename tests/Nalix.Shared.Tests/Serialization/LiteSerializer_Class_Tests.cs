using Nalix.Shared.Extensions;
using Nalix.Shared.Messaging;
using Nalix.Shared.Serialization;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Serialization;

public class LiteSerializer_ClassTest
{
    [Fact]
    public void Serialize_And_Deserialize_Should_Preserve_All_Fields()
    {
        var original = new BinaryPacket
        {
            OpCode = 456,
            Data = [10, 20, 30, 40, 50]
        };

        var bytes = LiteSerializer.Serialize(original);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        BinaryPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Length, deserialized.Length);
        Assert.Equal(original.OpCode, deserialized.OpCode);
        Assert.Equal(original.Data, deserialized.Data);
    }

    [Fact]
    public void Serialize_And_Deserialize_EmptyPayload_Should_Work()
    {
        var original = new BinaryPacket
        {
            OpCode = 2,
            Data = []
        };

        var bytes = LiteSerializer.Serialize(original);

        Assert.NotNull(bytes);

        BinaryPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.Data);
    }

    [Fact]
    public void Serialize_And_Deserialize_NullPayload_Should_Work()
    {
        var original = new BinaryPacket
        {
            OpCode = 2,
            Data = null!
        };

        var bytes = LiteSerializer.Serialize(original);

        Assert.NotNull(bytes);

        BinaryPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        // Theo kiểu code, null array sẽ ra null luôn
        Assert.Null(deserialized.Data);
    }

    [Fact]
    public void Serialize_And_Deserialize_With_MaxPayload()
    {
        var payload = new System.Byte[32];
        for (System.Int32 i = 0; i < payload.Length; i++)
        {
            payload[i] = (System.Byte)i;
        }

        var original = new BinaryPacket
        {
            OpCode = 999,
            Data = payload
        };

        var bytes = LiteSerializer.Serialize(original);
        Assert.NotNull(bytes);

        BinaryPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Equal(payload, deserialized.Data);
    }

    [Fact]
    public void Serialize_And_Deserialize_Should_Not_Change_Reference_Type()
    {
        var original = new BinaryPacket
        {
            OpCode = 22,
            Data = [5, 6, 7]
        };

        var bytes = LiteSerializer.Serialize(original);
        BinaryPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotSame(original, deserialized);
        Assert.Equal(original.Length, deserialized.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(32)]
    public void Serialize_And_Deserialize_With_Different_Payload_Sizes(System.Int32 count)
    {
        var payload = new System.Byte[count];
        for (System.Int32 i = 0; i < count; i++)
        {
            payload[i] = (System.Byte)(i + 1);
        }

        var original = new BinaryPacket
        {
            OpCode = 1234,
            Data = payload
        };

        var bytes = LiteSerializer.Serialize(original);
        Assert.NotNull(bytes);

        BinaryPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Equal(payload, deserialized.Data);
    }

    [Fact]
    public void Deserialize_With_Empty_Buffer_Should_Throw()
    {
        var buffer = System.Array.Empty<System.Byte>();
        BinaryPacket packet = null;
        _ = Assert.Throws<System.ArgumentException>(() => LiteSerializer.Deserialize(buffer, ref packet));
    }

    [Fact]
    public void Serialize_And_Deserialize_With_Max_Min_Values()
    {
        var original = new BinaryPacket
        {
            OpCode = 127,
            Data = [0, 255, 128]
        };

        var bytes = LiteSerializer.Serialize(original);

        System.Diagnostics.Debug.WriteLine($"Serialized Bytes: {BitConverter.ToString(bytes)}");
        System.Diagnostics.Debug.WriteLine($"MN: {bytes.ReadMagicNumberLE()} - {original.MagicNumber}");
        System.Diagnostics.Debug.WriteLine($"OP: {bytes.ReadOpCodeLE()} - {original.OpCode}");
        //System.Diagnostics.Debug.WriteLine($"TP: {bytes.ReadTransportLE()}");
        //System.Diagnostics.Debug.WriteLine($"PY: {bytes.ReadPriorityLE()}");

        BinaryPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        System.Diagnostics.Debug.WriteLine($"OP: {deserialized.OpCode} - {original.OpCode}");

        Assert.NotNull(deserialized);
        Assert.Equal(original.Length, deserialized.Length);
        Assert.Equal(original.OpCode, deserialized.OpCode);
        Assert.Equal(original.Data, deserialized.Data);
    }
}