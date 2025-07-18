﻿using Nalix.Common.Constants;
using Nalix.Common.Exceptions;
using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;
using Nalix.Network.Package;
using Nalix.Shared.Serialization;
using System;
using System.Text;
using Xunit;

namespace Nalix.Tests.Network.Package;

public class PacketTests
{
    [Fact]
    public void Packet_Empty_HasDefaultValues()
    {
        var packet = Packet.Empty;

        Assert.Equal(0, packet.OpCode);
        Assert.Equal(PacketType.None, packet.Type);
        Assert.Equal(PacketFlags.None, packet.Flags);
        Assert.Equal(PacketPriority.Low, packet.Priority);
        Assert.True(packet.Timestamp >= 0);
        Assert.Equal(packet.Length, PacketSize.Header + packet.Payload.Length);
    }

    [Fact]
    public void Packet_Construct_WithStringPayload_EncodesUtf8()
    {
        string text = "Hello";
        ushort opCode = 456;

        var packet = new Packet(opCode, text);

        Assert.Equal(PacketType.String, packet.Type);
        Assert.Equal(Encoding.UTF8.GetByteCount(text) + PacketSize.Header, packet.Length);
    }

    [Fact]
    public void Packet_SerializeDeserialize_RoundTrip_ShouldMatch()
    {
        var original = new Packet(200, PacketType.String, PacketFlags.Encrypted, PacketPriority.High, new byte[] { 1, 2, 3 });

        var bytes = original.Serialize();

        Packet deserialized = default;
        LiteSerializer.Deserialize(bytes.Span, ref deserialized);

        Assert.Equal(original.OpCode, deserialized.OpCode);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Flags, deserialized.Flags);
        Assert.Equal(original.Priority, deserialized.Priority);
        Assert.Equal(original.Payload.ToArray(), deserialized.Payload.ToArray());
        Assert.Equal(original.Checksum, deserialized.Checksum);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
        Assert.True(deserialized.IsValid());
    }

    [Fact]
    public void Packet_IsValid_ReturnsTrueForCorrectChecksum()
    {
        var packet = new Packet(1, "valid");

        Assert.True(packet.IsValid());
    }

    [Fact]
    public void Packet_IsExpired_WorksCorrectly()
    {
        var packet = new Packet(99, "check");

        Assert.False(packet.IsExpired(TimeSpan.FromSeconds(5)));
        Assert.True(packet.IsExpired(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Packet_Dispose_DoesNotThrow()
    {
        var payload = new byte[PacketConstants.HeapAllocLimit + 1];
        var packet = new Packet(123, new Memory<byte>(payload));

        packet.Dispose();

        Assert.True(true); // just verifying no exception
    }

    [Fact]
    public void Packet_Equals_ReturnsTrueForIdenticalValues()
    {
        var a = new Packet(1, PacketType.Binary, PacketFlags.None, PacketPriority.Low, new byte[] { 1, 2, 3 });
        var b = new Packet(1, PacketType.Binary, PacketFlags.None, PacketPriority.Low, new byte[] { 1, 2, 3 });

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Packet_Equals_ReturnsFalseForDifferentContent()
    {
        var a = new Packet(1, new ReadOnlyMemory<byte>([1, 2]));
        var b = new Packet(1, new ReadOnlyMemory<byte>([1, 2, 3]));

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Packet_ToDetailedString_IncludesImportantFields()
    {
        var packet = new Packet(1234, new ReadOnlyMemory<byte>([1, 2, 3, 4]));

        string result = packet.ToDetailedString();

        Assert.Contains("Packet [1234]", result);
        Assert.Contains("Payload: 4 bytes", result);
    }

    [Fact]
    public void Packet_Throws_WhenPayloadTooLarge()
    {
        var tooLarge = new byte[PacketConstants.PacketSizeLimit + 1];

        // Explicitly specify the constructor to resolve ambiguity
        Assert.ThrowsAny<Exception>(() => new Packet(1, new ReadOnlyMemory<byte>(tooLarge)));
    }

    [Fact]
    public void Packet_Construct_WithFullParameters_SetsAllCorrectly()
    {
        var packet = new Packet(
            555,
            PacketType.String,
            PacketFlags.Compressed,
            PacketPriority.High,
            new byte[] { 1, 1, 2 });

        Assert.Equal(555, packet.OpCode);
        Assert.Equal(PacketType.String, packet.Type);
        Assert.Equal(PacketFlags.Compressed, packet.Flags);
        Assert.Equal(PacketPriority.High, packet.Priority);
        Assert.Equal(3, packet.Payload.Length);
    }

    [Fact]
    public void Packet_CanBeConstructed_WithPayload()
    {
        ushort opCode = 1234;
        byte[] payload = [0x01, 0x02, 0x03];

        // Act
        var packet = new Packet(opCode, new Memory<byte>(payload)); // Explicitly use Memory<byte> constructor

        // Assert
        Assert.Equal(opCode, packet.OpCode);
        Assert.Equal(PacketType.Binary, packet.Type);
        Assert.Equal(PacketFlags.None, packet.Flags);
        Assert.Equal(PacketPriority.Low, packet.Priority);
        Assert.Equal(payload.Length, packet.Payload.Length);
        Assert.True(packet.Payload.Span.SequenceEqual(payload));
    }

    [Fact]
    public void Packet_Serialize_Deserialize_ShouldReturnEquivalentPacket()
    {
        // Arrange
        var packet = new Packet(100, PacketType.String, PacketFlags.None, PacketPriority.High, new Memory<byte>([0x10, 0x20, 0x30])); // Explicitly use Memory<byte>

        // Act
        var bytes = packet.Serialize();

        Packet packet2 = default;
        LiteSerializer.Deserialize(bytes.Span, ref packet2);

        // Assert
        Assert.Equal(packet.OpCode, packet2.OpCode);
        Assert.Equal(packet.Type, packet2.Type);
        Assert.Equal(packet.Flags, packet2.Flags);
        Assert.Equal(packet.Priority, packet2.Priority);
        Assert.True(packet.Payload.Span.SequenceEqual(packet2.Payload.Span));
        Assert.True(packet2.IsValid());
    }

    [Fact]
    public void Packet_Checksum_IsValid()
    {
        // Arrange
        var packet = new Packet(100, new Memory<byte>([0xAA, 0xBB, 0xCC])); // Explicitly use Memory<byte>

        // Act
        var isValid = packet.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Packet_Equals_ShouldWorkForIdenticalPackets()
    {
        // Arrange
        var a = new Packet(1, new Memory<byte>([1, 2, 3])); // Explicitly use Memory<byte>
        var b = new Packet(1, new Memory<byte>([1, 2, 3])); // Explicitly use Memory<byte>

        // Act & Assert
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Packet_Equals_ShouldDetectDifferentPayloads()
    {
        // Arrange
        var a = new Packet(1, new Memory<byte>([1, 2, 3])); // Explicitly use Memory<byte>
        var b = new Packet(1, new Memory<byte>([1, 2, 3])); // Explicitly use Memory<byte>

        // Act & Assert
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Packet_ToDetailedString_ShouldContainOpCode()
    {
        // Arrange
        var packet = new Packet(2222, new Memory<byte>([0x1, 0x2, 0x3, 0x4])); // Explicitly use Memory<byte>

        // Act
        string detail = packet.ToDetailedString();

        // Assert
        Assert.Contains("Packet [2222]", detail);
        Assert.Contains("Payload: 4 bytes", detail);
    }

    [Fact]
    public void CreatePacket_WithStringPayload_SetsCorrectProperties()
    {
        // Arrange
        ushort opCode = 1000;
        string payload = "Test payload";

        // Act
        var packet = new Packet(opCode, payload);

        // Assert
        Assert.Equal(opCode, packet.OpCode);
        Assert.Equal(PacketType.String, packet.Type);
        Assert.Equal(PacketFlags.None, packet.Flags);
        Assert.Equal(PacketPriority.Low, packet.Priority);
        Assert.Equal(Encoding.UTF8.GetBytes(payload).Length + PacketSize.Header, packet.Length);
        Assert.True(packet.Timestamp > 0);
        Assert.True(packet.IsValid());
    }

    [Fact]
    public void CreatePacket_WithByteArrayPayload_SetsCorrectProperties()
    {
        // Arrange
        ushort opCode = 1001;
        byte[] payload = Encoding.UTF8.GetBytes("Test byte array");

        // Act
        var packet = new Packet(opCode, new Memory<byte>(payload));

        // Assert
        Assert.Equal(opCode, packet.OpCode);
        Assert.Equal(PacketType.Binary, packet.Type);
        Assert.Equal(PacketFlags.None, packet.Flags);
        Assert.Equal(PacketPriority.Low, packet.Priority);
        Assert.Equal(payload.Length + PacketSize.Header, packet.Length);
        Assert.True(packet.IsValid());
    }

    [Fact]
    public void CreatePacket_TooLargePayload_ThrowsPackageException()
    {
        // Arrange
        ushort opCode = 1002;
        var payload = new byte[PacketConstants.PacketSizeLimit + 1];

        // Act & Assert
        Assert.Throws<PackageException>(() => new Packet(opCode, new Memory<byte>(payload)));
    }

    [Fact]
    public void SerializeDeserialize_RoundTrip_MaintainsDataIntegrity()
    {
        // Arrange
        ushort opCode = 1003;
        string payload = "Serialization test";
        var originalPacket = new Packet(opCode, payload);

        // Act
        var serialized = originalPacket.Serialize();

        Packet deserialized = default;
        LiteSerializer.Deserialize(serialized.Span, ref deserialized);

        // Assert
        Assert.Equal(originalPacket.OpCode, deserialized.OpCode);
        Assert.Equal(originalPacket.Type, deserialized.Type);
        Assert.Equal(originalPacket.Flags, deserialized.Flags);
        Assert.Equal(originalPacket.Priority, deserialized.Priority);
        Assert.Equal(originalPacket.Length, deserialized.Length);
        Assert.Equal(originalPacket.Checksum, deserialized.Checksum);
        Assert.Equal(originalPacket.Timestamp, deserialized.Timestamp);
        Assert.Equal(originalPacket.Payload.ToArray(), deserialized.Payload.ToArray());
    }

    [Fact]
    public void IsValid_WithValidChecksum_ReturnsTrue()
    {
        // Arrange
        var packet = new Packet(1004, "Valid checksum test");

        // Act
        bool isValid = packet.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsExpired_WithTimeout_ReturnsCorrectResult()
    {
        // Arrange
        var packet = new Packet(1005, "Expiration test");

        // Act
        bool notExpired = packet.IsExpired(TimeSpan.FromMilliseconds(100));
        bool expired = packet.IsExpired(TimeSpan.FromMilliseconds(-100));

        // Assert
        Assert.False(notExpired);
        Assert.True(expired);
    }

    [Fact]
    public void Dispose_LargePayload_ReturnsToPool()
    {
        // Arrange
        var payload = new byte[PacketConstants.HeapAllocLimit + 1];
        var packet = new Packet(1006, new Memory<byte>(payload));

        // Act
        packet.Dispose();

        // Assert - We can't directly test ArrayPool return, but we can verify no exceptions
        // and that the operation completes
        Assert.True(true); // If we reach here without exception, disposal worked
    }

    [Fact]
    public void EmptyPacket_HasDefaultValues()
    {
        // Act
        var packet = Packet.Empty;

        // Assert
        Assert.Equal(0, packet.OpCode);
        Assert.Equal(PacketType.None, packet.Type);
        Assert.Equal(PacketFlags.None, packet.Flags);
        Assert.Equal(PacketPriority.Low, packet.Priority);
        Assert.Empty(packet.Payload.ToArray());
    }

    [Fact]
    public void CreatePacket_WithFullParameters_SetsAllProperties()
    {
        // Arrange
        ushort opCode = 1007;
        var type = PacketType.Binary;
        var flags = PacketFlags.Compressed;
        var priority = PacketPriority.High;
        var payload = Encoding.UTF8.GetBytes("Full test");

        // Act
        var packet = new Packet(opCode, type, flags, priority, new Memory<byte>(payload));

        // Assert
        Assert.Equal(opCode, packet.OpCode);
        Assert.Equal(type, packet.Type);
        Assert.Equal(flags, packet.Flags);
        Assert.Equal(priority, packet.Priority);
        Assert.Equal(payload, packet.Payload.ToArray());
    }
}