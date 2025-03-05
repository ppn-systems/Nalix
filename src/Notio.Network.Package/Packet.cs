using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Cryptography.Integrity;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Metadata;
using Notio.Network.Package.Utilities;
using Notio.Network.Package.Utilities.Data;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Network.Package;

/// <summary>
/// Represents an immutable network packet with metadata and payload.
/// This high-performance struct is optimized for efficient serialization and transmission.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("Packet {Id}: Type={Type}, Command={Command}, Length={Length}")]
public readonly struct Packet : IPacket, IEquatable<Packet>, IDisposable
{
    // Cache the max packet size locally to avoid field access costs
    private const int MaxPacketSize = PacketConstants.MaxPacketSize;
    private const int MaxStackAllocSize = PacketConstants.MaxStackAllocSize;
    private const int MaxHeapAllocSize = PacketConstants.MaxHeapAllocSize;

    /// <summary>
    /// Gets the total length of the packet including header and payload.
    /// </summary>
    public ushort Length => (ushort)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the packet identifier, which is a unique identifier for this packet instance.
    /// </summary>
    public byte Id { get; }

    /// <summary>
    /// Gets the packet type, which specifies the kind of packet.
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// Gets the flags associated with the packet, used for additional control information.
    /// </summary>
    public byte Flags { get; }

    /// <summary>
    /// Gets the priority level of the packet, which affects how the packet is processed.
    /// </summary>
    public byte Priority { get; }

    /// <summary>
    /// Gets the command associated with the packet, which specifies an operation type.
    /// </summary>
    public ushort Command { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created in microseconds since system startup.
    /// </summary>
    public ulong Timestamp { get; }

    /// <summary>
    /// Gets the CRC32 checksum of the packet payload for integrity validation.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the payload data being transmitted in this packet.
    /// </summary>
    public Memory<byte> Payload { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific command and payload.
    /// </summary>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort command, Memory<byte> payload)
        : this(PacketType.None, PacketFlags.None, PacketPriority.None, command, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for type, flags, and priority.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(PacketType type, PacketFlags flags, PacketPriority priority, ushort command, Memory<byte> payload)
        : this((byte)type, (byte)flags, (byte)priority, command, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with type, flags, priority, command, and payload.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(byte type, byte flags, byte priority, ushort command, Memory<byte> payload)
        : this((byte)0, type, flags, priority, command, TimeUtils.GetMicrosecondTimestamp(), 0, payload, true)
    {
    }

    /// <summary>
    /// Internal constructor used by the packet serializer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Packet(byte id, byte type, byte flags, byte priority, ushort command, Memory<byte> payload)
        : this(id, type, flags, priority, command, TimeUtils.GetMicrosecondTimestamp(), 0, payload, true)
    {
    }

    /// <summary>
    /// Creates a new packet with full control over all fields.
    /// </summary>
    /// <param name="id">The packet identifier.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="timestamp">The packet timestamp.</param>
    /// <param name="checksum">The packet checksum.</param>
    /// <param name="payload">The packet payload.</param>
    /// <param name="computeChecksum">If true, computes the checksum; otherwise uses the provided value.</param>
    /// <exception cref="PackageException">Thrown when the packet size exceeds the maximum allowed size.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Packet(
        byte id,
        byte type,
        byte flags,
        byte priority,
        ushort command,
        ulong timestamp,
        uint checksum,
        Memory<byte> payload,
        bool computeChecksum = false)
    {
        // Validate payload size
        if (payload.Length + PacketSize.Header > MaxPacketSize)
        {
            throw new PackageException($"Packet size ({payload.Length + PacketSize.Header} bytes) " +
                                      $"exceeds maximum allowed size ({MaxPacketSize} bytes)");
        }

        // Initialize fields
        Id = id == 0 ? (byte)(timestamp % byte.MaxValue) : id;
        Type = type;
        Flags = flags;
        Command = command;
        Priority = priority;
        Timestamp = timestamp;

        // Create a secure copy of the payload to prevent external modification
        Payload = DataMemory.Allocate(payload);

        // Compute checksum only if needed
        Checksum = computeChecksum ? Crc32.HashToUInt32(Payload.Span) : checksum;
    }

    /// <summary>
    /// Verifies the packet's checksum against the computed checksum of the payload.
    /// </summary>
    /// <returns>True if the checksum is valid; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid() => Crc32.HashToUInt32(Payload.Span) == Checksum;

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to check against.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpired(TimeSpan timeout)
    {
        // Use direct math operations for better performance
        ulong currentTime = TimeUtils.GetMicrosecondTimestamp();
        ulong timeoutMicroseconds = (ulong)(timeout.TotalMilliseconds * 1000);

        // Handle potential overflow (rare but possible)
        if (currentTime < Timestamp)
        {
            return false;
        }

        return (currentTime - Timestamp) > timeoutMicroseconds;
    }

    /// <summary>
    /// Creates a copy of this packet with new flags.
    /// </summary>
    /// <param name="newFlags">The new flags.</param>
    /// <returns>A new packet with the updated flags.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet WithFlags(byte newFlags) =>
        new(Id, Type, newFlags, Priority, Command, Timestamp, Checksum, Payload);

    /// <summary>
    /// Creates a new packet that is a copy of this one but with a new payload.
    /// </summary>
    /// <param name="newPayload">The new payload to use.</param>
    /// <returns>A new packet with the same metadata but different payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet WithPayload(Memory<byte> newPayload)
        => new(Id, Type, Flags, Priority, Command, Timestamp, 0, newPayload, true);

    /// <summary>
    /// Returns a hash code for this packet.
    /// </summary>
    /// <returns>A hash code value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        // Initial hash with key fields
        int hash = Type;
        hash = (hash * 397) ^ Flags;
        hash = (hash * 397) ^ Command;
        hash = (hash * 397) ^ Priority;

        // Add payload contribution - handle cases efficiently
        if (Payload.Length > 0)
        {
            // Create a robust hash that includes size, start, and end of payload
            hash = (hash * 397) ^ Payload.Length;

            if (Payload.Length <= MaxStackAllocSize)
            {
                // For small payloads, use the full content
                ReadOnlySpan<byte> span = Payload.Span;
                for (int i = 0; i < span.Length; i += sizeof(int))
                {
                    int chunk = 0;
                    int bytesToRead = Math.Min(sizeof(int), span.Length - i);
                    for (int j = 0; j < bytesToRead; j++)
                    {
                        chunk |= span[i + j] << (j * 8);
                    }
                    hash = (hash * 397) ^ chunk;
                }
            }
            else
            {
                // For larger payloads, use beginning, middle and end samples
                ReadOnlySpan<byte> span = Payload.Span;

                // Beginning (up to 64 bytes)
                int bytesToSample = Math.Min(64, span.Length);
                for (int i = 0; i < bytesToSample; i += sizeof(int))
                {
                    int chunk = 0;
                    int bytesToRead = Math.Min(sizeof(int), bytesToSample - i);
                    for (int j = 0; j < bytesToRead; j++)
                    {
                        chunk |= span[i + j] << (j * 8);
                    }
                    hash = (hash * 397) ^ chunk;
                }

                // End (up to 64 bytes)
                if (span.Length > 128)
                {
                    int startIndex = span.Length - 64;
                    for (int i = 0; i < 64; i += sizeof(int))
                    {
                        int chunk = 0;
                        int bytesToRead = Math.Min(sizeof(int), 64 - i);
                        for (int j = 0; j < bytesToRead; j++)
                        {
                            chunk |= span[startIndex + i + j] << (j * 8);
                        }
                        hash = (hash * 397) ^ chunk;
                    }
                }

                // Include the checksum for additional mixing
                hash = (hash * 397) ^ (int)Checksum;
            }
        }

        return hash;
    }

    /// <summary>
    /// Releases any resources used by this packet, returning rented arrays to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        // Only return large arrays to the pool
        if (Payload.Length > MaxHeapAllocSize &&
            MemoryMarshal.TryGetArray<byte>(Payload, out var segment) &&
            segment.Array is { } array)
        {
            ArrayPool<byte>.Shared.Return(array, clearArray: true);
        }
    }

    /// <summary>
    /// Compares this packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(IPacket? other)
    {
        if (other is null)
            return false;

        // Quick field comparison first
        if (Type != other.Type ||
            Flags != other.Flags ||
            Command != other.Command ||
            Priority != other.Priority ||
            Payload.Length != other.Payload.Length)
        {
            return false;
        }

        // Only compare payload contents if everything else matches
        return Payload.Span.SequenceEqual(other.Payload.Span);
    }

    /// <summary>
    /// Compares this packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    public bool Equals(Packet other) =>
        Type == other.Type &&
        Flags == other.Flags &&
        Command == other.Command &&
        Priority == other.Priority &&
        Payload.Length == other.Payload.Length &&
        Payload.Span.SequenceEqual(other.Payload.Span);

    /// <summary>
    /// Compares this packet with another object for equality.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the objects are equal; otherwise, false.</returns>
    public override bool Equals(object? obj) =>
        obj is Packet packet && Equals(packet);

    /// <summary>
    /// Determines whether two packets are equal.
    /// </summary>
    /// <param name="left">The first packet.</param>
    /// <param name="right">The second packet.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    public static bool operator ==(Packet left, Packet right) => left.Equals(right);

    /// <summary>
    /// Determines whether two packets are not equal.
    /// </summary>
    /// <param name="left">The first packet.</param>
    /// <param name="right">The second packet.</param>
    /// <returns>True if the packets are not equal; otherwise, false.</returns>
    public static bool operator !=(Packet left, Packet right) => !left.Equals(right);

    /// <summary>
    /// Creates a packet from raw binary data.
    /// </summary>
    /// <param name="data">The binary data containing a serialized packet.</param>
    /// <returns>A new packet deserialized from the data.</returns>
    /// <exception cref="PackageException">Thrown if the data is invalid or corrupted.</exception>
    public static Packet FromRawData(ReadOnlySpan<byte> data)
        => PacketSerializer.ReadPacketFast(data);

    /// <summary>
    /// Writes this packet to a binary buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="PackageException">Thrown if the buffer is too small.</exception>
    public int WriteTo(Span<byte> buffer)
        => PacketSerializer.WritePacketFast(buffer, this);

    /// <summary>
    /// Creates an acknowledgment packet for this packet.
    /// </summary>
    /// <returns>A new packet configured as an acknowledgment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet CreateAcknowledgment()
        => new(
            id: Id,
            type: (byte)PacketType.Acknowledgment,
            flags: (byte)(Flags | (byte)PacketFlags.Acknowledged),
            priority: Priority,
            command: Command,
            timestamp: TimeUtils.GetMicrosecondTimestamp(),
            checksum: 0,
            payload: Memory<byte>.Empty,
            computeChecksum: true
        );

    /// <summary>
    /// Gets a string representation of this packet for debugging purposes.
    /// </summary>
    /// <returns>A string that represents this packet.</returns>
    public override string ToString()
        => $"Packet ID={Id}, Type={Type}, Command={Command}, " +
           $"Flags={Flags}, Priority={Priority}, Size={Length} bytes";
}
