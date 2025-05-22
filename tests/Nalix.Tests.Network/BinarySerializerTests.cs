using Nalix.Common.Serialization;
using Nalix.Serialization;
using System;
using Xunit;

namespace Nalix.Tests.Network;

public class BinarySerializerTests
{
    private struct TestStruct
    {
        [FieldOrder(1)]
        public int IntValue;

        [FieldOrder(2)]
        public byte ByteValue;

        [FieldOrder(3)]
        private byte _a;
    }

    [Fact]
    public void SerializeAndDeserialize_PrimitiveAndStringFields_ShouldBeEqual()
    {
        var original = new TestStruct
        {
            IntValue = 1,
            ByteValue = 0x7F,
        };

        int size = BinarySerializer<TestStruct>.GetSize(in original);
        byte[] buffer = new byte[size];
        BinarySerializer<TestStruct>.Serialize(in original, buffer.AsSpan()); // Explicitly use Span<byte>

        var result = BinarySerializer<TestStruct>.Deserialize(buffer.AsSpan()); // Explicitly use ReadOnlySpan<byte>

        Assert.Equal(original.IntValue, result.IntValue);
        Assert.Equal(original.ByteValue, result.ByteValue);
    }

    [Fact]
    public void SerializeAndDeserialize_NullStringAndNullBytes_ShouldWork()
    {
        var original = new TestStruct
        {
            IntValue = 1,
            ByteValue = 2,
        };

        int size = BinarySerializer<TestStruct>.GetSize(in original);
        byte[] buffer = new byte[size];
        BinarySerializer<TestStruct>.Serialize(in original, buffer.AsSpan()); // Explicitly use Span<byte>

        var result = BinarySerializer<TestStruct>.Deserialize(buffer.AsSpan()); // Explicitly use ReadOnlySpan<byte>

        Assert.Equal(original.IntValue, result.IntValue);
        Assert.Equal(original.ByteValue, result.ByteValue);
    }
}
