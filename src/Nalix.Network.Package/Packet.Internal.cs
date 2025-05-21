using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;
using Nalix.Network.Package.Engine.Internal;
using Nalix.Shared.Time;

namespace Nalix.Network.Package;

public readonly partial struct Packet
{
    /// <summary>
    /// Internal constructor used by the packet serializer.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal Packet(
        ushort id,
        byte number,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.Memory<byte> payload)
        : this(id, number, 0, 0, type, flags, priority, payload)
    {
    }

    /// <summary>
    /// Creates a new packet with full control over all fields.
    /// </summary>
    /// <exception cref="Common.Exceptions.PackageException">Thrown when the packet size exceeds the maximum allowed size.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal Packet(
        ushort id,
        byte number,
        uint checksum,
        long timestamp,
        byte type,
        byte flags,
        byte priority,
        System.Memory<byte> payload)
        : this(id, number, checksum, timestamp, (PacketType)type,
              (PacketFlags)flags, (PacketPriority)priority, payload)
    {
    }

    /// <summary>
    /// Creates a new packet with full control over all fields.
    /// </summary>
    /// <exception cref="Common.Exceptions.PackageException">Thrown when the packet size exceeds the maximum allowed size.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal Packet(
        ushort id,
        byte number,
        uint checksum,
        long timestamp,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.Memory<byte> payload)
    {
        // Validate payload size
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new Common.Exceptions.PackageException(
                $"Packet size ({payload.Length + PacketSize.Header} bytes) " +
                $"exceeds maximum allowed size ({MaxPacketSize} bytes)");

        // Initialize fields
        Id = id;
        Type = type;
        Flags = flags;
        Priority = priority;
        Number = number == 0 ? (byte)(timestamp % byte.MaxValue) : number;
        Timestamp = timestamp == 0 ? Clock.UnixMillisecondsNow() : timestamp;

        // Create a secure copy of the payload to prevent external modification
        Payload = MemoryAllocator.Allocate(payload);

        // Compute checksum only if needed
        Checksum = checksum == 0 ? Integrity.Crc32.Compute(Payload.Span) : checksum;

        unchecked
        {
            _hash = ((ulong)number << 56)
                  | ((ulong)id << 40)
                  | ((ulong)type << 32)
                  | ((ulong)flags << 24)
                  | ((ulong)timestamp & 0x000000FFFFFFUL);
        }
    }
}
