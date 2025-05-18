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
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        byte number,
        System.Memory<byte> payload)
        : this(id, 0, Clock.UnixTicksNow(), type, flags, priority, number, payload, true)
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
        uint checksum,
        long timestamp,
        byte type,
        byte flags,
        byte priority,
        byte number,
        System.Memory<byte> payload,
        bool computeChecksum = false)
        : this(id, checksum, timestamp,
              (PacketType)type, (PacketFlags)flags,
              (PacketPriority)priority, number, payload, computeChecksum)
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
        uint checksum,
        long timestamp,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        byte number,
        System.Memory<byte> payload,
        bool computeChecksum = false)
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
        Timestamp = timestamp;
        Number = number == 0 ? (byte)(timestamp % byte.MaxValue) : number;

        // Create a secure copy of the payload to prevent external modification
        Payload = MemoryAllocator.Allocate(payload);

        // Compute checksum only if needed
        Checksum = computeChecksum ? Integrity.Crc32.Compute(Payload.Span) : checksum;

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
