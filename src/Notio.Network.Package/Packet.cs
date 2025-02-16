using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Cryptography.Integrity;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Metadata;
using Notio.Network.Package.Utilities;
using Notio.Network.Package.Utilities.Data;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Network.Package;

/// <summary>
/// Represents a packet structure that can be pooled and disposed.
/// This packet contains metadata such as the packet type, flags, priority, command, timestamp, and checksum,
/// along with a payload containing the data for transmission.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Packet : IPacket
{
    /// <summary>
    /// Gets the total length of the packet including header and payload.
    /// </summary>
    /// <returns>
    /// The total length in bytes.
    /// </returns>
    public readonly ushort Length => (ushort)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the packet identifier, which is a unique identifier for this packet instance.
    /// </summary>
    public byte Id { get; }

    /// <summary>
    /// Gets the packet type, which specifies the kind of packet.
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// Gets or sets the flags associated with the packet, used for additional control or state information.
    /// </summary>
    public byte Flags { get; }

    /// <summary>
    /// Gets the priority level of the packet, which can affect how the packet is processed or prioritized.
    /// </summary>
    public byte Priority { get; }

    /// <summary>
    /// Gets the command associated with the packet, which can specify an operation or request type.
    /// </summary>
    public ushort Command { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created. This is a unique timestamp based on the system's current time.
    /// </summary>
    public ulong Timestamp { get; }

    /// <summary>
    /// Gets or sets the checksum of the packet, computed based on the payload. Used for integrity validation.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the payload of the packet, which contains the data being transmitted.
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
    /// Initializes a new instance of the <see cref="Packet"/> struct with type, flags, priority, command, and payload.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload (data).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(byte type, byte flags, byte priority, ushort command, Memory<byte> payload)
        : this(null, type, flags, priority, command, null, null, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct with specified packet metadata and payload.
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Packet(byte id, byte type, byte flags, byte priority, ushort command, Memory<byte> payload)
        : this(id, type, flags, priority, command, null, null, payload)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Packet(
        byte? id, byte type, byte flags, byte priority,
        ushort command, ulong? timestamp, uint? checksum, Memory<byte> payload)
    {
        if (payload.Length + PacketSize.Header > PacketConstants.MaxPacketSize)
            throw new PackageException("Packet size exceeds 64KB limit");

        this.Timestamp = timestamp ?? PacketTimeUtils.GetMicrosecondTimestamp();
        this.Id = id ?? (byte)(this.Timestamp % byte.MaxValue);
        this.Type = type;
        this.Flags = flags;
        this.Command = command;
        this.Priority = priority;
        this.Payload = DataMemory.Allocate(payload);
        this.Checksum = checksum ?? Crc32.HashToUInt32(payload.Span);
    }

    /// <summary>
    /// Verifies the packet's checksum matches the computed checksum based on the payload.
    /// </summary>
    /// <returns>True if the checksum matches; otherwise, false.</returns>
    public readonly bool IsValid() => Crc32.HashToUInt32(Payload.Span) == Checksum;

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to compare against the packet's timestamp.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    public readonly bool IsExpired(TimeSpan timeout) =>
        (PacketTimeUtils.GetMicrosecondTimestamp() - Timestamp) > (ulong)timeout.TotalMilliseconds;

    /// <summary>
    /// Compares the current packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(IPacket? other)
    {
        if (other is null || Type != other.Type || Flags != other.Flags ||
            Command != other.Command || Priority != other.Priority)
            return false;

        return Payload.Span.SequenceEqual(other.Payload.Span);
    }

    /// <summary>
    /// Compares the current packet with another object for equality.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the object is a <see cref="Packet"/> and is equal to the current packet; otherwise, false.</returns>
    public override readonly bool Equals(object? obj) => obj is Packet other && Equals(other);

    /// <summary>
    /// Returns a hash code for the current packet based on its fields and payload.
    /// </summary>
    /// <returns>A hash code for the current packet.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Flags);
        hash.Add(Command);
        hash.Add(Priority);

        if (this.Payload.Length > 0)
        {
            if (this.Payload.Length <= PacketConstants.MaxStackAllocSize)
            {
                Span<byte> buffer = stackalloc byte[PacketConstants.MaxStackAllocSize];
                buffer.Clear();
                this.Payload.Span.CopyTo(buffer);
                hash.Add(MemoryMarshal.Read<ulong>(buffer));
            }
            else
            {
                hash.Add(this.Payload.Length);
                hash.AddBytes(this.Payload.Span[..PacketConstants.MaxStackAllocSize]);
                hash.AddBytes(this.Payload.Span[^PacketConstants.MaxStackAllocSize..]);
            }
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Releases the resources used by the packet.
    /// If the packet is pooled, it returns the memory to the ArrayPool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        if (this.Payload.Length > PacketConstants.MaxHeapAllocSize &&
            MemoryMarshal.TryGetArray<byte>(this.Payload, out var segment) && segment.Array is { } array)
            ArrayPool<byte>.Shared.Return(array);

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public static bool operator ==(Packet left, Packet right)
        => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(Packet left, Packet right)
        => !(left == right);
}
