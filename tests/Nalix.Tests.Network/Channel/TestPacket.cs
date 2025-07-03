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
    public ushort OpCode { get; set; }
    public byte Number { get; set; }
    public uint Checksum { get; set; }
    public long Timestamp { get; set; }
    public PacketType Type { get; set; }
    public PacketFlags Flags { get; set; }
    public ReadOnlyMemory<byte> Payload { get; set; }

    public int Hash => ComputeHash();

    // Default constructor with reasonable defaults for testing
    public TestPacket()
    {
        Priority = PacketPriority.Low;
        Length = 128;
        OpCode = 1;
        Number = 1;
        Checksum = 123456; // Example checksum
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Current timestamp
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

        return OpCode == other.OpCode &&
               Number == other.Number &&
               Priority == other.Priority &&
               Checksum == other.Checksum;
    }

    public bool IsExpired(TimeSpan timeout)
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return currentTime - Timestamp > timeout.TotalMilliseconds;
    }

    public bool IsExpired(long timeout)
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return currentTime - Timestamp > timeout;
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
        BitConverter.TryWriteBytes(buffer[1..3], OpCode);
        BitConverter.TryWriteBytes(buffer[3..7], Checksum);
        buffer[7] = (byte)Priority;
        Payload.Span.CopyTo(buffer[8..]); // Assume payload starts at offset 8
    }

    public override string ToString()
    {
        return $"Packet[Priority={Priority}, OpCode={OpCode}, Number={Number}, Checksum={Checksum}, Timestamp={Timestamp}]";
    }

    private int ComputeHash()
    {
        int hash = 0;
        hash |= (int)Number << 56;
        hash |= (int)OpCode << 40;
        hash |= (int)Type << 32;
        hash |= (int)Flags << 16;
        hash |= (int)Timestamp & int.MaxValue; // Use only the lowest 40 bits of the timestamp
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