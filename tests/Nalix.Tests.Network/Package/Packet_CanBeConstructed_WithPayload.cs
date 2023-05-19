using Nalix.Common.Package.Enums;
using Nalix.Network.Package;
using Nalix.Shared.Serialization;
using System;
using Xunit;

namespace Nalix.Tests.Network.Package;

public class PacketTests
{
    [Fact]
    public void Packet_CanBeConstructed_WithPayload()
    {
        ushort opCode = 1234;
        byte[] payload = { 0x01, 0x02, 0x03 };

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
        var packet = new Packet(100, new Memory<byte>(new byte[] { 0xAA, 0xBB, 0xCC })); // Explicitly use Memory<byte>

        // Act
        var isValid = packet.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Packet_Equals_ShouldWorkForIdenticalPackets()
    {
        // Arrange
        var a = new Packet(1, new Memory<byte>(new byte[] { 1, 2, 3 })); // Explicitly use Memory<byte>
        var b = new Packet(1, new Memory<byte>(new byte[] { 1, 2, 3 })); // Explicitly use Memory<byte>

        // Act & Assert
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Packet_Equals_ShouldDetectDifferentPayloads()
    {
        // Arrange
        var a = new Packet(1, new Memory<byte>(new byte[] { 1, 2, 3 })); // Explicitly use Memory<byte>
        var b = new Packet(1, new Memory<byte>(new byte[] { 1, 2, 4 })); // Explicitly use Memory<byte>

        // Act & Assert
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Packet_ToDetailedString_ShouldContainOpCode()
    {
        // Arrange
        var packet = new Packet(2222, new Memory<byte>(new byte[] { 0x1, 0x2, 0x3, 0x4 })); // Explicitly use Memory<byte>

        // Act
        string detail = packet.ToDetailedString();

        // Assert
        Assert.Contains("Packet [2222]", detail);
        Assert.Contains("Payload: 4 bytes", detail);
    }
}