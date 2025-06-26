using Nalix.Common.Constants;
using Nalix.Common.Package.Enums;
using Nalix.Common.Package.Metadata;
using Nalix.Network.Package.Engine;
using Nalix.Network.Package.Engine.Internal;
using Nalix.Shared.Time;
using System;

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
        System.Memory<System.Byte> payload)
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
        System.Memory<System.Byte> payload)
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
        System.Memory<System.Byte> payload)
    {
        // Validate payload size
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new Common.Exceptions.PackageException(
                $"Packet size ({payload.Length + PacketSize.Header} bytes) " +
                $"exceeds maximum allowed size ({MaxPacketSize} bytes)");

        // Initialize fields
        OpCode = opCode;
        Type = type;
        Flags = flags;
        Priority = priority;
        Number = number == 0 ? (System.Byte)(timestamp % System.Byte.MaxValue) : number;
        Timestamp = timestamp == 0 ? Clock.UnixMillisecondsNow() : timestamp;

        // Create a secure copy of the payload to prevent external modification
        Payload = MemoryAllocator.Allocate(payload);

        // Compute checksum only if needed
        Checksum = checksum == 0 ? Integrity.Crc32.Compute(Payload.Span) : checksum;

        _hash = GetHashCode();

        if (Payload.Length > PacketConstants.HeapAllocLimit)
        {
            try
            {
                // Register large packets for garbage collection
                PacketAutoDisposer.Register(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register packet for disposal: {ex.Message}");
            }
        }
    }
}
