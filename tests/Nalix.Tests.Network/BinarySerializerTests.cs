using Nalix.Common.Attributes;
using Nalix.Serialization;
using System;
using Xunit;

namespace Nalix.Tests.Network;

public class BinarySerializerTests
{
    private struct TestStruct
    {
        [SerializableField(1)]
        public int IntValue;

        [SerializableField(2)]
        public byte ByteValue;

        [SerializableField(3)]
        public string StringValue;

        [SerializableField(4)]
        private bool _flag;

        public bool Flag { get => _flag; set => _flag = value; }

        [SerializableField(5)]
        public byte[] Bytes;
    }

    [Fact]
    public void SerializeAndDeserialize_PrimitiveAndStringFields_ShouldBeEqual()
    {
        var original = new TestStruct
        {
            IntValue = 1,
            ByteValue = 0x7F,
            StringValue = "Xin chào .NET",
            Flag = true,
            Bytes = new byte[] { 1, 2, 3, 4 }
        };

        int size = BinarySerializer<TestStruct>.GetSize(in original);
        byte[] buffer = new byte[size];
        BinarySerializer<TestStruct>.Serialize(in original, buffer.AsSpan()); // Explicitly use Span<byte>

        var result = BinarySerializer<TestStruct>.Deserialize(buffer.AsSpan()); // Explicitly use ReadOnlySpan<byte>

        Assert.Equal(original.IntValue, result.IntValue);
        Assert.Equal(original.ByteValue, result.ByteValue);
        Assert.Equal(original.StringValue, result.StringValue);
        Assert.Equal(original.Flag, result.Flag);
        Assert.Equal(original.Bytes, result.Bytes);
    }

    [Fact]
    public void SerializeAndDeserialize_NullStringAndNullBytes_ShouldWork()
    {
        var original = new TestStruct
        {
            IntValue = 1,
            ByteValue = 2,
            StringValue = null,
            Flag = false,
            Bytes = null
        };

        int size = BinarySerializer<TestStruct>.GetSize(in original);
        byte[] buffer = new byte[size];
        BinarySerializer<TestStruct>.Serialize(in original, buffer.AsSpan()); // Explicitly use Span<byte>

        var result = BinarySerializer<TestStruct>.Deserialize(buffer.AsSpan()); // Explicitly use ReadOnlySpan<byte>

        Assert.Null(result.StringValue);
        Assert.Null(result.Bytes);
        Assert.Equal(original.IntValue, result.IntValue);
        Assert.Equal(original.ByteValue, result.ByteValue);
        Assert.Equal(original.Flag, result.Flag);
    }
}
