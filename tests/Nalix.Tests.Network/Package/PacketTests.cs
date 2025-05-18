using Nalix.Common.Constants;
using Nalix.Common.Cryptography;
using Nalix.Network.Package;
using Nalix.Network.Package.Engine;
using Nalix.Network.Package.Engine.Serialization;
using Nalix.Network.Package.Extensions;
using System;
using Xunit;

namespace Nalix.Tests.Network.Package;

public class PacketTests
{
    [Fact]
    public void Packet_Should_Initialize_With_Valid_Data()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];

        // Act
        Packet packet = new(id, payload);

        // Assert
        Assert.Equal(id, packet.Id);
        Assert.Equal(payload.Length, packet.Payload.Length);
    }

    [Fact]
    public void Packet_Should_Calculate_Valid_Checksum()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];
        Packet packet = new(id, payload);

        // Act
        bool isValid = packet.IsValid();

        // Assert
        Assert.True(isValid, "The checksum should be valid.");
    }

    [Fact]
    public void Packet_Should_Expire_After_Timeout()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];
        Packet packet = new(id, payload);
        TimeSpan timeout = TimeSpan.FromMilliseconds(1);

        // Act
        System.Threading.Thread.Sleep(2); // Simulate delay
        bool isExpired = packet.IsExpired(timeout);

        // Assert
        Assert.True(isExpired, "The packet should have expired after the timeout.");
    }

    [Fact]
    public void PacketSerialization_Should_Serialize_And_Deserialize_Correctly()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];
        Packet packet = new(id, payload);

        // Act
        Memory<byte> serialized = packet.Serialize();
        Packet deserializedPacket = PacketSerializer.Deserialize(serialized.Span);

        // Assert
        Assert.Equal(packet.Id, deserializedPacket.Id);
        Assert.Equal(packet.Payload.Length, deserializedPacket.Payload.Length);
    }

    [Fact]
    public void Packet_Should_Not_Expire_If_Within_Timeout()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];
        Packet packet = new(id, payload);
        TimeSpan timeout = TimeSpan.FromSeconds(2);

        // Act
        bool isExpired = packet.IsExpired(timeout);

        // Assert
        Assert.False(isExpired, "The packet should not have expired within the timeout.");
    }

    [Fact]
    public void Packet_Should_Serialize_And_Deserialize_Correctly()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];
        Packet packet = new(id, payload);

        // Act
        Memory<byte> serialized = packet.Serialize();
        Packet deserializedPacket = PacketSerializer.Deserialize(serialized.Span);

        // Assert
        Assert.Equal(packet.Id, deserializedPacket.Id);
        Assert.Equal(packet.Payload.Length, deserializedPacket.Payload.Length);
    }

    [Fact]
    public void Packet_Should_Throw_Exception_When_Payload_Too_Large()
    {
        // Arrange
        ushort id = 1;
        byte[] largePayload = new byte[PacketConstants.PacketSizeLimit + 1];

        // Act & Assert
        Assert.Throws<Common.Exceptions.PackageException>(() => new Packet(id, largePayload));
    }

    [Fact]
    public void Packet_Should_Compress_And_Decompress_Correctly()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];
        Packet packet = new(id, payload);

        // Act
        Packet compressed = PacketCompact.Compress(packet);
        Packet decompressed = PacketCompact.Decompress(compressed);

        // Assert
        Assert.Equal(packet.Id, decompressed.Id);
        Assert.Equal(packet.Payload.Length, decompressed.Payload.Length);
    }

    [Fact]
    public void Packet_Should_Encrypt_And_Decrypt_Correctly()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];
        Packet packet = new(id, payload);
        byte[] key = new byte[32];
        var algorithm = EncryptionType.Speck;

        // Act
        Packet encrypted = packet.EncryptPayload(key, algorithm);
        Packet decrypted = encrypted.DecryptPayload(key, algorithm);

        // Assert
        Assert.Equal(packet.Id, decrypted.Id);
        Assert.Equal(packet.Payload.Length, decrypted.Payload.Length);
    }

    [Fact]
    public void Packet_ToString_Should_Return_Human_Readable_String()
    {
        // Arrange
        ushort id = 1;
        byte[] payload = [1, 2, 3, 4];
        Packet packet = new(id, payload);

        // Act
        string result = packet.ToString();

        // Assert
        Assert.Contains($"Packet Number={packet.Number}", result);
        Assert.Contains($"Type={packet.Type}", result);
        Assert.Contains($"Payload={packet.Payload.Length} bytes", result);
    }
}
