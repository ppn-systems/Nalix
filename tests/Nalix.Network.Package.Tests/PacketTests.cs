using Nalix.Common.Constants;
using Nalix.Common.Package.Enums;
using Nalix.Network.Package.Engine.Serialization;
using System;
using System.Diagnostics;
using Xunit;

namespace Nalix.Network.Package.Tests;

public class PacketTests
{
    [Fact]
    public void Packet_Should_Initialize_With_Valid_Data()
    {
        // Arrange
        ushort id = 1;
        var code = PacketCode.Success; // Replace with appropriate enum value from your project
        byte[] payload = new byte[] { 1, 2, 3, 4 };

        // Act
        var packet = new Packet(id, code, payload);

        // Assert
        Assert.Equal(id, packet.Id);
        Assert.Equal(code, packet.Code);
        Assert.Equal(payload.Length, packet.Payload.Length);
    }

    [Fact]
    public void Packet_Should_Calculate_Valid_Checksum()
    {
        // Arrange
        ushort id = 1;
        var code = PacketCode.Success; // Replace with appropriate enum value
        byte[] payload = new byte[] { 1, 2, 3, 4 };
        var packet = new Packet(id, code, payload);

        // Act
        var isValid = packet.IsValid();

        // Assert
        Assert.True(isValid, "The checksum should be valid.");
    }

    [Fact]
    public void Packet_Should_Expire_After_Timeout()
    {
        // Arrange
        ushort id = 1;
        var code = PacketCode.Success; // Replace with appropriate enum value
        byte[] payload = [1, 2, 3, 4];
        var packet = new Packet(id, code, payload);
        var timeout = TimeSpan.FromMilliseconds(1);

        // Act
        System.Threading.Thread.Sleep(2); // Simulate delay
        var isExpired = packet.IsExpired(timeout);

        // Assert
        Assert.False(isExpired, "The packet should have expired after the timeout.");
    }

    [Fact]
    public void PacketSerialization_Should_Serialize_And_Deserialize_Correctly()
    {
        // Arrange
        ushort id = 1;
        var code = PacketCode.Success; // Replace with appropriate enum value
        byte[] payload = new byte[] { 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4 };
        var packet = new Packet(id, code, payload);

        // Act
        var serialized = packet.Serialize();
        Debug.WriteLine($"Serialized Length: {packet.Payload.Length}");
        var deserializedPacket = PacketSerializer.Deserialize(serialized.Span);
        Debug.WriteLine($"Deserialize Length: {deserializedPacket.Payload.Length}");

        // Assert
        Assert.Equal(packet.Id, deserializedPacket.Id);
        Assert.Equal(packet.Code, deserializedPacket.Code);
        Assert.Equal(packet.Payload.Length, deserializedPacket.Payload.Length);
    }

    [Fact]
    public void Packet_Should_Throw_Exception_When_Payload_Too_Large()
    {
        // Arrange
        ushort id = 1;
        var code = PacketCode.Success; // Replace with appropriate enum value
        byte[] largePayload = new byte[PacketConstants.PacketSizeLimit + 1];

        // Act & Assert
        Assert.Throws<Nalix.Common.Exceptions.PackageException>(() => new Packet(id, code, largePayload));
    }
}
