using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;
using Nalix.Cryptography.Checksums;
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
        System.UInt16 opCode,
        System.Byte number,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.ReadOnlyMemory<System.Byte> payload)
        : this(opCode, number, 0, 0, type, flags, priority, payload)
    {
    }

    /// <summary>
    /// Creates a new packet with full control over all fields.
    /// </summary>
    /// <exception cref="Common.Exceptions.PackageException">Thrown when the packet size exceeds the maximum allowed size.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal Packet(
        System.UInt16 opCode,
        System.Byte number,
        System.UInt32 checksum,
        System.Int64 timestamp,
        System.Byte type,
        System.Byte flags,
        System.Byte priority,
        System.ReadOnlyMemory<System.Byte> payload)
        : this(opCode, number, checksum, timestamp, (PacketType)type,
              (PacketFlags)flags, (PacketPriority)priority, payload)
    {
    }

    /// <summary>
    /// Creates a new packet with full control over all fields.
    /// </summary>
    /// <param name="opCode">The operation code that identifies the packet type.</param>
    /// <param name="number">The packet number, used for sequencing.</param>
    /// <param name="checksum">The CRC32 checksum of the payload. If 0, it will be computed automatically.</param>
    /// <param name="timestamp">The Unix timestamp in milliseconds. If 0, the current time is used.</param>
    /// <param name="type">The type of the packet.</param>
    /// <param name="flags">The flags associated with the packet.</param>
    /// <param name="priority">The priority of the packet.</param>
    /// <param name="payload">The payload data of the packet.</param>
    /// <exception cref="Common.Exceptions.PackageException">Thrown when the packet size exceeds the maximum allowed size.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal Packet(
        System.UInt16 opCode,
        System.Byte number,
        System.UInt32 checksum,
        System.Int64 timestamp,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        System.ReadOnlyMemory<System.Byte> payload)
    {
        // Validate payload size
        if (payload.Length + PacketSize.Header > MaxPacketSize)
        {
            throw new Common.Exceptions.PackageException(
                $"Packet size ({payload.Length + PacketSize.Header} bytes) " +
                $"exceeds maximum allowed size ({MaxPacketSize} bytes)");
        }

        // Create a secure copy of the payload to prevent external modification
        _buffer = MemoryVault.Allocate(payload.Span);

        // Initialize fields
        OpCode = opCode;
        Type = type;
        Flags = flags;
        Priority = priority;
        Payload = _buffer.Memory;
        Checksum = checksum == 0 ? Crc32.Compute(Payload.Span) : checksum;
        Timestamp = timestamp == 0 ? Clock.UnixMillisecondsNow() : timestamp;
        Number = number == 0 ? (System.Byte)(Timestamp % System.Byte.MaxValue) : number;

        _hash = GetHashCode();
    }
}