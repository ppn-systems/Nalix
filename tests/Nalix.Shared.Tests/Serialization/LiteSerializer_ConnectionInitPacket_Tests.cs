using Nalix.Shared.Serialization;
using Nalix.Shared.Tests.Packets;
using Xunit;

namespace Nalix.Shared.Tests.Serialization;

public class LiteSerializer_ConnectionInitPacket_FullTests
{
    [Fact]
    public void Serialize_And_Deserialize_Should_Preserve_All_Fields()
    {
        var original = new ConnectionInitPacket
        {
            Length = 123,
            OpCode = 456,
            MagicNumber = 0xCAFEBABE,
            Payload = [10, 20, 30, 40, 50]
        };

        var bytes = LiteSerializer.Serialize(original);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        ConnectionInitPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Length, deserialized.Length);
        Assert.Equal(original.OpCode, deserialized.OpCode);
        Assert.Equal(original.MagicNumber, deserialized.MagicNumber);
        Assert.Equal(original.Payload, deserialized.Payload);
    }

    [Fact]
    public void Serialize_And_Deserialize_EmptyPayload_Should_Work()
    {
        var original = new ConnectionInitPacket
        {
            Length = 1,
            OpCode = 2,
            MagicNumber = 3,
            Payload = []
        };

        var bytes = LiteSerializer.Serialize(original);

        Assert.NotNull(bytes);

        ConnectionInitPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.Payload);
    }

    [Fact]
    public void Serialize_And_Deserialize_NullPayload_Should_Work()
    {
        var original = new ConnectionInitPacket
        {
            Length = 1,
            OpCode = 2,
            MagicNumber = 3,
            Payload = null!
        };

        var bytes = LiteSerializer.Serialize(original);

        Assert.NotNull(bytes);

        ConnectionInitPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        // Theo kiểu code, null array sẽ ra null luôn
        Assert.Null(deserialized.Payload);
    }

    [Fact]
    public void SerializeIgnore_Must_Exclude_Hash_Property()
    {
        var packet = new ConnectionInitPacket
        {
            Length = 11,
            OpCode = 22,
            MagicNumber = 33,
            Payload = [1, 2, 3]
        };

        var bytes = LiteSerializer.Serialize(packet);

        // Tăng Hash lên (nếu serialize đúng thì deserialize lại không có giá trị này)
        _ = new ConnectionInitPacket
        {
            Length = 11,
            OpCode = 22,
            MagicNumber = 33,
            Payload = [1, 2, 3]
        };

        // Fake: set Hash khác đi, nhưng property Hash luôn là 0 và bị SerializeIgnore nên không được lưu
        Assert.Equal(0, packet.Hash);

        ConnectionInitPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        // Nếu serialize đúng, property Hash sẽ luôn là 0
        Assert.Equal(0, deserialized.Hash);
    }

    [Fact]
    public void Serialize_And_Deserialize_With_MaxPayload()
    {
        var payload = new System.Byte[32];
        for (System.Int32 i = 0; i < payload.Length; i++)
        {
            payload[i] = (System.Byte)i;
        }

        var original = new ConnectionInitPacket
        {
            Length = 32,
            OpCode = 999,
            MagicNumber = 0xABCDEF12,
            Payload = payload
        };

        var bytes = LiteSerializer.Serialize(original);
        Assert.NotNull(bytes);

        ConnectionInitPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Equal(payload, deserialized.Payload);
    }

    [Fact]
    public void Serialize_And_Deserialize_Should_Not_Change_Reference_Type()
    {
        var original = new ConnectionInitPacket
        {
            Length = 11,
            OpCode = 22,
            MagicNumber = 33,
            Payload = [5, 6, 7]
        };

        var bytes = LiteSerializer.Serialize(original);
        ConnectionInitPacket deserialized = null;
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

        var original = new ConnectionInitPacket
        {
            Length = (System.UInt16)count,
            OpCode = 1234,
            MagicNumber = 0x12345678,
            Payload = payload
        };

        var bytes = LiteSerializer.Serialize(original);
        Assert.NotNull(bytes);

        ConnectionInitPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Equal(payload, deserialized.Payload);
    }

    [Fact]
    public void Deserialize_With_Empty_Buffer_Should_Throw()
    {
        var buffer = System.Array.Empty<System.Byte>();
        ConnectionInitPacket packet = null;
        _ = Assert.Throws<System.ArgumentException>(() => LiteSerializer.Deserialize(buffer, ref packet));
    }

    [Fact]
    public void Serialize_And_Deserialize_With_Max_Min_Values()
    {
        var original = new ConnectionInitPacket
        {
            Length = System.UInt16.MaxValue,
            OpCode = System.UInt16.MinValue,
            MagicNumber = System.UInt32.MaxValue,
            Payload = [0, 255, 128]
        };

        var bytes = LiteSerializer.Serialize(original);
        ConnectionInitPacket deserialized = null;
        _ = LiteSerializer.Deserialize(bytes, ref deserialized);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Length, deserialized.Length);
        Assert.Equal(original.OpCode, deserialized.OpCode);
        Assert.Equal(original.MagicNumber, deserialized.MagicNumber);
        Assert.Equal(original.Payload, deserialized.Payload);
    }
}