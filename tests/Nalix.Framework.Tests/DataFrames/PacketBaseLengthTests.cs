using System;
using System.Collections.Generic;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed class PacketBaseLengthTests
{
    [Theory]
    [InlineData("hello")]
    [InlineData("xin chao")]
    [InlineData("Xin chao Viet Nam")]
    [InlineData("xin chao \u4f60\u597d")]
    public void LengthWhenPacketContainsStringMatchesSerializedByteCount(string text)
    {
        StringPacket packet = new()
        {
            Message = text,
            SequenceId = 42
        };

        byte[] bytes = packet.Serialize();

        Assert.Equal(bytes.Length, packet.Length);

        byte[] buffer = new byte[packet.Length];
        int written = packet.Serialize(buffer);

        Assert.Equal(packet.Length, written);
        Assert.Equal(bytes, buffer);
    }

    [Fact]
    public void LengthWhenPacketContainsNestedPacketMatchesSerializedByteCount()
    {
        ParentPacket packet = new()
        {
            Child = new ChildPacket
            {
                Value = 123456789,
                SequenceId = 7
            }
        };

        byte[] bytes = packet.Serialize();

        Assert.Equal(bytes.Length, packet.Length);

        byte[] buffer = new byte[packet.Length];
        int written = packet.Serialize(buffer);

        Assert.Equal(packet.Length, written);
        Assert.Equal(bytes, buffer);
    }

    [Fact]
    public void LengthWhenNestedPacketIsNullMatchesSerializedByteCount()
    {
        ParentPacket packet = new()
        {
            Child = null
        };

        byte[] bytes = packet.Serialize();

        Assert.Equal(bytes.Length, packet.Length);
        Assert.Equal(PacketConstants.HeaderSize + sizeof(byte), packet.Length);
    }

    [Fact]
    public void SerializeWhenBufferIsSmallerThanDynamicLengthThrowsArgumentException()
    {
        StringPacket packet = new()
        {
            Message = "dynamic payload"
        };

        byte[] buffer = new byte[packet.Length - 1];
        ArgumentException ex = Assert.Throws<ArgumentException>(() => packet.Serialize(buffer));

        Assert.Contains("Buffer too small", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LengthWhenStringUsesSerializeDynamicSizeAttributeMatchesSerializedByteCount()
    {
        DynamicHintStringPacket packet = new()
        {
            Message = "xin chao"
        };

        byte[] bytes = packet.Serialize();
        Assert.Equal(bytes.Length, packet.Length);

        byte[] buffer = new byte[packet.Length];
        int written = packet.Serialize(buffer);
        Assert.Equal(packet.Length, written);
        Assert.Equal(bytes, buffer);
    }

    [Fact]
    public void LengthWhenPacketContainsListMatchesSerializedByteCount()
    {
        ListPacket packet = new()
        {
            Values = [1, 2, 3, 4, 5]
        };

        byte[] bytes = packet.Serialize();
        Assert.Equal(bytes.Length, packet.Length);

        byte[] buffer = new byte[packet.Length];
        int written = packet.Serialize(buffer);
        Assert.Equal(packet.Length, written);
        Assert.Equal(bytes, buffer);
    }

    [Fact]
    public void LengthWhenPacketContainsDictionaryMatchesSerializedByteCount()
    {
        DictionaryPacket packet = new()
        {
            Values = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["alpha"] = 1,
                ["beta"] = 2,
                ["xin chao"] = 3
            }
        };

        byte[] bytes = packet.Serialize();
        Assert.Equal(bytes.Length, packet.Length);

        byte[] buffer = new byte[packet.Length];
        int written = packet.Serialize(buffer);
        Assert.Equal(packet.Length, written);
        Assert.Equal(bytes, buffer);
    }

    [SerializePackable(SerializeLayout.Sequential)]
    private sealed class StringPacket : PacketBase<StringPacket>
    {
        public string Message { get; set; } = string.Empty;

        public static new StringPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<StringPacket>.Deserialize(buffer);
    }

    [SerializePackable(SerializeLayout.Sequential)]
    private sealed class ChildPacket : PacketBase<ChildPacket>
    {
        [SerializeOrder(0)]
        public int Value { get; set; }

        public static new ChildPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<ChildPacket>.Deserialize(buffer);
    }

    [SerializePackable(SerializeLayout.Sequential)]
    private sealed class ParentPacket : PacketBase<ParentPacket>
    {
        public ChildPacket? Child { get; set; }

        public static new ParentPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<ParentPacket>.Deserialize(buffer);
    }

    [SerializePackable(SerializeLayout.Sequential)]
    private sealed class ListPacket : PacketBase<ListPacket>
    {
        public List<int>? Values { get; set; }

        public static new ListPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<ListPacket>.Deserialize(buffer);
    }

    [SerializePackable(SerializeLayout.Sequential)]
    private sealed class DictionaryPacket : PacketBase<DictionaryPacket>
    {
        public Dictionary<string, int>? Values { get; set; }

        public static new DictionaryPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DictionaryPacket>.Deserialize(buffer);
    }

    [SerializePackable(SerializeLayout.Sequential)]
    private sealed class DynamicHintStringPacket : PacketBase<DynamicHintStringPacket>
    {
        [SerializeDynamicSize(64)]
        public string Message { get; set; } = string.Empty;

        public static new DynamicHintStringPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DynamicHintStringPacket>.Deserialize(buffer);
    }
}
