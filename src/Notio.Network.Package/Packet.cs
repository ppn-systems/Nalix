using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Common.Package.Metadata;
using Notio.Defaults;
using Notio.Integrity;
using Notio.Network.Package.Serialization;
using Notio.Utilities;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization.Metadata;

namespace Notio.Network.Package;

/// <summary>
/// Represents an immutable network packet with metadata and payload.
/// This high-performance struct is optimized for efficient serialization and transmission.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("Packet {Number}: Number={Number}, Type={Type}, Number={Number}, Length={Length}")]
public readonly struct Packet : IPacket, IEquatable<Packet>, IDisposable
{
    #region Constants

    // Cache the max packet size locally to avoid field access costs
    private const int MaxPacketSize = PacketConstants.PacketSizeLimit;
    private const int MaxHeapAllocSize = DefaultConstants.HeapAllocThreshold;
    private const int MaxStackAllocSize = DefaultConstants.StackAllocThreshold;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the total length of the packet including header and payload.
    /// </summary>
    public ushort Length => (ushort)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the Number associated with the packet, which specifies an operation type.
    /// </summary>
    public ushort Id { get; }

    /// <summary>
    /// Gets the packet identifier, which is a unique identifier for this packet instance.
    /// </summary>
    public byte Number { get; }

    /// <summary>
    /// Gets the CRC32 checksum of the packet payload for integrity validation.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created in microseconds since system startup.
    /// </summary>
    public ulong Timestamp { get; }

    /// <summary>
    /// Gets the packet type, which specifies the kind of packet.
    /// </summary>
    public PacketType Type { get; }

    /// <summary>
    /// Gets the flags associated with the packet, used for additional control information.
    /// </summary>
    public PacketFlags Flags { get; }

    /// <summary>
    /// Gets the priority level of the packet, which affects how the packet is processed.
    /// </summary>
    public PacketPriority Priority { get; }

    /// <summary>
    /// Gets the payload data being transmitted in this packet.
    /// </summary>
    public Memory<byte> Payload { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, byte[] payload)
    : this(id, new Memory<byte>(payload))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, Span<byte> payload)
    : this(id, new Memory<byte>(payload.ToArray()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with a specific Number and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, Memory<byte> payload)
        : this(id, PacketType.None, PacketFlags.None, PacketPriority.None, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for flags and priority.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="s">The packet payload as a UTF-8 encoded string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketFlags flags, PacketPriority priority, string s)
        : this(id, PacketType.String, flags, priority, Encoding.UTF8.GetBytes(s))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with the specified flags, priority, Number, and payload.
    /// </summary>
    /// <param name="id">The Number identifier for the packet.</param>
    /// <param name="flags">The packet flags indicating specific properties of the packet.</param>
    /// <param name="priority">The priority level of the packet.</param>
    /// <param name="obj">The payload of the packet.</param>
    /// <param name="jsonTypeInfo">The metadata used for JSON serialization.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(PacketFlags flags, PacketPriority priority,
        ushort id, object obj, JsonTypeInfo<object> jsonTypeInfo)
        : this(id, PacketType.Object, flags, priority, JsonBuffer.SerializeToMemory(obj, jsonTypeInfo))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with type, flags, priority, Number, and payload.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, byte type, byte flags, byte priority, Memory<byte> payload)
        : this(id, (PacketType)type, (PacketFlags)flags, (PacketPriority)priority, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified enum values for type, flags, and priority.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort id, PacketType type, PacketFlags flags, PacketPriority priority, Memory<byte> payload)
        : this(id, 0, MicrosecondClock.GetTimestamp(), 0, type, flags, priority, payload, true)
    {
    }

    #endregion

    #region Internal Constructors

    /// <summary>
    /// Internal constructor used by the packet serializer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Packet(
        ushort id,
        byte number,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
        Memory<byte> payload)
        : this(id,
              0,
              MicrosecondClock.GetTimestamp(),
              number,
              type,
              flags,
              priority,
              payload,
              true)
    {
    }

    /// <summary>
    /// Creates a new packet with full control over all fields.
    /// </summary>
    /// /// <param name="id">The packet Number.</param>
    /// <param name="number">The packet identifier.</param>
    /// <param name="checksum">The packet checksum.</param>
    /// <param name="timestamp">The packet timestamp.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload.</param>
    /// <param name="computeChecksum">If true, computes the checksum; otherwise uses the provided value.</param>
    /// <exception cref="PackageException">Thrown when the packet size exceeds the maximum allowed size.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Packet(
        ushort id,
        uint checksum,
        ulong timestamp,
        byte number,
        byte type,
        byte flags,
        byte priority,
        Memory<byte> payload,
        bool computeChecksum = false)
        : this(id,
              checksum,
              timestamp,
              number,
              (PacketType)type,
              (PacketFlags)flags,
              (PacketPriority)priority,
              payload,
              computeChecksum)
    {
    }

    /// <summary>
    /// Creates a new packet with full control over all fields.
    /// </summary>
    /// <param name="id">The packet Number.</param>
    /// <param name="checksum">The packet checksum.</param>
    /// <param name="timestamp">The packet timestamp.</param>
    /// <param name="number">The packet identifier.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="payload">The packet payload.</param>
    /// <param name="computeChecksum">If true, computes the checksum; otherwise uses the provided value.</param>
    /// <exception cref="PackageException">Thrown when the packet size exceeds the maximum allowed size.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Packet(
        ushort id,
        uint checksum,
        ulong timestamp,
        byte number,
        PacketType type,
        PacketFlags flags,
        PacketPriority priority,
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
        Number = number == 0 ? (byte)(timestamp % byte.MaxValue) : number;
        Type = type;
        Flags = flags;
        Id = id;
        Priority = priority;
        Timestamp = timestamp;

        // Create a secure copy of the payload to prevent external modification
        Payload = MemoryAllocator.Allocate(payload);

        // Compute checksum only if needed
        Checksum = computeChecksum ? Crc32.Compute(Payload.Span) : checksum;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// Verifies the packet'obj checksum against the computed checksum of the payload.
    /// </summary>
    /// <returns>True if the checksum is valid; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid() => Crc32.Compute(Payload.Span) == Checksum;

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to check against.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpired(TimeSpan timeout)
    {
        // Use direct math operations for better performance
        ulong currentTime = MicrosecondClock.GetTimestamp();
        ulong timeoutMicroseconds = (ulong)(timeout.TotalMilliseconds * 1000);

        // Handle potential overflow (rare but possible)
        if (currentTime < Timestamp)
            return false;

        return (currentTime - Timestamp) > timeoutMicroseconds;
    }

    #endregion

    #region Modification Methods

    /// <summary>
    /// Creates a copy of this packet with new flags.
    /// </summary>
    /// <param name="newFlags">The new flags.</param>
    /// <returns>A new packet with the updated flags.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet WithFlags(PacketFlags newFlags) =>
        new(Id, Checksum, Timestamp, Number, Type, newFlags, Priority, Payload);

    /// <summary>
    /// Creates a new packet that is a copy of this one but with a new payload.
    /// </summary>
    /// <param name="newPayload">The new payload to use.</param>
    /// <returns>A new packet with the same metadata but different payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet WithPayload(Memory<byte> newPayload)
        => new(Id, 0, Timestamp, Number, Type, Flags, Priority, newPayload, true);

    #endregion

    #region Serialization Methods

    /// <summary>
    /// Serializes the packet into a new byte array.
    /// </summary>
    /// <returns>
    /// A byte array containing the serialized representation of the packet.
    /// </returns>
    public Memory<byte> Serialize() => PacketSerializer.Serialize(this);

    /// <summary>
    /// Serializes the packet into the provided buffer.
    /// </summary>
    /// <param name="buffer">
    /// A span of bytes to write the serialized packet into. The buffer must be large enough to hold the entire packet.
    /// </param>
    /// <exception cref="PackageException">
    /// Thrown if the buffer is too small to contain the serialized packet.
    /// </exception>
    public void Serialize(Span<byte> buffer) => PacketSerializer.WritePacketUnsafe(buffer, this);

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
    /// <returns>The Number of bytes written.</returns>
    /// <exception cref="PackageException">Thrown if the buffer is too small.</exception>
    public int WriteTo(Span<byte> buffer)
        => PacketSerializer.WritePacketFast(buffer, this);

    #endregion

    #region Equality Methods

    /// <summary>
    /// Returns a hash code for this packet.
    /// </summary>
    /// <returns>A hash code value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        // Initial hash with key fields
        int hash = (byte)Type;
        hash = (hash * 397) ^ (byte)Flags;
        hash = (hash * 397) ^ (byte)Id;
        hash = (hash * 397) ^ (byte)Priority;

        // For small payloads, use the full content
        ReadOnlySpan<byte> span = Payload.Span;

        // Add payload contribution - handle cases efficiently
        if (span.Length > 0)
        {
            // Create a robust hash that includes size, start, and end of payload
            hash = (hash * 397) ^ span.Length;

            if (span.Length <= MaxStackAllocSize)
            {
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
            Id != other.Id ||
            Priority != other.Priority ||
            Payload.Length != other.Payload.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> span1 = Payload.Span;
        ReadOnlySpan<byte> span2 = other.Payload.Span;

        if (span1.Length < 32)
            return span1.SequenceEqual(span2);

        return span1[..16].SequenceEqual(span2[..16]) && span1[^16..].SequenceEqual(span2[^16..]);
    }

    /// <summary>
    /// Compares this packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    public bool Equals(Packet other) =>
        Type == other.Type &&
        Flags == other.Flags &&
        Id == other.Id &&
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

    #endregion

    #region Cleanup Methods

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

    #endregion

    #region String Methods

    /// <summary>
    /// Converts the packet's data into a human-readable, detailed string representation.
    /// </summary>
    /// <remarks>
    /// This method provides a structured view of the packet's contents, including:
    /// - Number, type, flags, Number, priority, timestamp, and checksum.
    /// - Payload size and, if applicable, a hex dump of the payload data.
    /// - If the payload is larger than 32 bytes, only the first and last 16 bytes are displayed.
    /// </remarks>
    /// <returns>
    /// A formatted string containing detailed packet information.
    /// </returns>
    public string ToDetailedString()
    {
        StringBuilder sb = new();
        sb.AppendLine($"Packet [{Id}]:");
        sb.AppendLine($"  Type: {(PacketType)Type}");
        sb.AppendLine($"  Flags: {(PacketFlags)Flags}");
        sb.AppendLine($"  Number: 0x{Number:X4}");
        sb.AppendLine($"  Priority: {(PacketPriority)Priority}");
        sb.AppendLine($"  Timestamp: {Timestamp}");
        sb.AppendLine($"  Checksum: 0x{Checksum:X8} (Valid: {IsValid()})");
        sb.AppendLine($"  Payload: {Payload.Length} bytes");

        if (Payload.Length > 0)
        {
            sb.Append("  Data: ");

            if (Payload.Length <= 32)
                for (int i = 0; i < Payload.Length; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");
            else
            {
                for (int i = 0; i < 16; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");

                sb.Append("... ");

                for (int i = Payload.Length - 16; i < Payload.Length; i++)
                    sb.Append($"{Payload.Span[i]:X2} ");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a string representation of this packet for debugging purposes.
    /// </summary>
    /// <returns>A string that represents this packet.</returns>
    public override string ToString()
        => $"Packet Number={Number}, Type={Type}, Number={Id}, " +
           $"Flags={Flags}, Priority={Priority}, Timestamp={Timestamp}, " +
           $"Checksum={IsValid()}, Payload={Payload.Length} bytes, Size={Length} bytes";

    #endregion
}
