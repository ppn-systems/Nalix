using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using System;

namespace Nalix.Tests.Network.Channel;

/// <summary>
/// A mock implementation of the IPacket interface for testing purposes.
/// </summary>
public class TestPacket : IPacket
{
    public PacketPriority Priority { get; set; }
    public ushort Length { get; set; }
    public ushort Id { get; set; }
    public byte Number { get; set; }
    public uint Checksum { get; set; }
    public long Timestamp { get; set; }
    public PacketCode Code { get; set; }
    public PacketType Type { get; set; }
    public PacketFlags Flags { get; set; }
    public Memory<byte> Payload { get; set; }

    public long Hash => ComputeHash();

    // Default constructor with reasonable defaults for testing
    public TestPacket()
    {
        Priority = PacketPriority.Low;
        Length = 128;
        Id = 1;
        Number = 1;
        Checksum = 123456; // Example checksum
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Current timestamp
        Code = PacketCode.Success;
        Type = PacketType.Binary;
        Flags = PacketFlags.None;
        Payload = new Memory<byte>(new byte[Length]);
    }

    public void Dispose()
    {
        Payload = Memory<byte>.Empty;
        GC.SuppressFinalize(this);
    }

    public bool Equals(IPacket? other)
    {
        if (other == null) return false;

        return Id == other.Id &&
               Number == other.Number &&
               Priority == other.Priority &&
               Checksum == other.Checksum;
    }

    public bool IsExpired(TimeSpan timeout)
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return currentTime - Timestamp > timeout.TotalMilliseconds;
    }

    public bool IsValid()
    {
        uint calculatedChecksum = ComputeChecksum(Payload.Span);
        return Checksum == calculatedChecksum;
    }

    public Memory<byte> Serialize()
    {
        var buffer = new byte[Length];
        Serialize(buffer.AsSpan());
        return new Memory<byte>(buffer);
    }

    public void Serialize(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer is too small to serialize the packet.");

        buffer[0] = Number;
        BitConverter.TryWriteBytes(buffer[1..3], Id);
        BitConverter.TryWriteBytes(buffer[3..7], Checksum);
        buffer[7] = (byte)Priority;
        Payload.Span.CopyTo(buffer[8..]); // Assume payload starts at offset 8
    }

    public override string ToString()
    {
        return $"Packet[Priority={Priority}, Id={Id}, Number={Number}, Checksum={Checksum}, Timestamp={Timestamp}]";
    }

    private long ComputeHash()
    {
        long hash = 0;
        hash |= (long)Number << 56;
        hash |= (long)Id << 40;
        hash |= (long)Type << 32;
        hash |= (long)Code << 24;
        hash |= (long)Flags << 16;
        hash |= Timestamp & 0xFFFFFFFFFF; // Use only the lowest 40 bits of the timestamp
        return hash;
    }

    private static uint ComputeChecksum(ReadOnlySpan<byte> data)
    {
        uint checksum = 0;
        foreach (byte b in data)
        {
            checksum += b;
        }
        return checksum;
    }
}
