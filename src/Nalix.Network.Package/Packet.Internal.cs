using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;

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
        PacketCode code,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        byte number,
        System.Memory<byte> payload)
        : this(id, 0, Utilities.PreciseTimeClock.GetTimestamp(),
              code, type, flags, priority, number, payload, true)
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
        ulong timestamp,
        ushort code,
        byte type,
        byte flags,
        byte priority,
        byte number,
        System.Memory<byte> payload,
        bool computeChecksum = false)
        : this(id, checksum, timestamp,
              (PacketCode)code, (PacketType)type,
              (PacketFlags)flags, (PacketPriority)priority,
              number, payload, computeChecksum)
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
        ulong timestamp,
        PacketCode code,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        byte number,
        System.Memory<byte> payload,
        bool computeChecksum = false)
    {
        // Validate payload size
        if (payload.Length + PacketSize.Header > MaxPacketSize)
        {
            throw new Common.Exceptions.PackageException(
                $"Packet size ({payload.Length + PacketSize.Header} bytes) exceeds maximum allowed size ({MaxPacketSize} bytes)");
        }

        // Initialize fields
        Id = id;
        Timestamp = timestamp;

        Code = code;
        Type = type;
        Flags = flags;
        Priority = priority;

        Number = number == 0 ? (byte)(timestamp % byte.MaxValue) : number;

        // Create a secure copy of the payload to prevent external modification
        Payload = Utilities.BufferAllocator.Allocate(payload);

        // Compute checksum only if needed
        Checksum = computeChecksum ? Integrity.Crc32.Compute(Payload.Span) : checksum;

        _hash = unchecked(
            ((ulong)Number << 56) |
            ((ulong)Id << 40) |
            ((ulong)Type << 32) |
            ((ulong)Code << 24) |
            ((ulong)Flags << 16) |
            (Timestamp & 0xFFFFFFFFFF)
        );
    }
}
