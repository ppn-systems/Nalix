using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Cryptography.Ciphers.Integrity;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Extensions;
using Notio.Network.Package.Metadata;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Network.Package;

/// <summary>
/// Represents a packet structure that can be pooled and disposed.
/// This packet contains metadata and a payload with associated checksum and flags.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Packet : IPacket, IEquatable<Packet>
{
    private const int StackAllocThreshold = 256;
    private const int HeapAllocThreshold = 1024;

    /// <summary>
    /// The minimum packet size (in bytes).
    /// </summary>
    public const ushort MinPacketSize = 256;

    /// <summary>
    /// The maximum allowed packet size (in bytes), 64KB.
    /// </summary>
    public const ushort MaxPacketSize = ushort.MaxValue;

    /// <summary>
    /// Threshold for optimized vectorized memory comparison.
    /// </summary>
    private const int Vector256Size = 32;

    private readonly bool IsPooled;

    /// <summary>
    /// Gets the total length of the packet.
    /// </summary>
    public readonly ushort Length => (ushort)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the packet identifier.
    /// </summary>
    public byte Id { get; }

    /// <summary>
    /// Gets the packet type.
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// Gets or sets the packet flags.
    /// </summary>
    public byte Flags { get; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    public byte Priority { get; }

    /// <summary>
    /// Gets the command associated with the packet.
    /// </summary>
    public ushort Command { get; }

    /// <summary>
    /// Gets the timestamp when the packet was created.
    /// </summary>
    public ulong Timestamp { get; }

    /// <summary>
    /// Gets or sets the checksum of the packet, computed based on the payload.
    /// </summary>
    public uint Checksum { get; }

    /// <summary>
    /// Gets the payload of the packet.
    /// </summary>
    public Memory<byte> Payload { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IPacket"/> struct.
    /// </summary>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(ushort command, Memory<byte> payload)
        : this(PacketType.None, PacketFlags.None, PacketPriority.None, command, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IPacket"/> struct.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(byte type, byte flags, byte priority, ushort command, Memory<byte> payload)
        : this(null, type, flags, priority, command, null, null, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IPacket"/> struct.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(PacketType type, PacketFlags flags, PacketPriority priority, ushort command, Memory<byte> payload)
        : this((byte)type, (byte)flags, (byte)priority, command, payload)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IPacket"/> struct.
    /// </summary>
    /// <param name="id">The packet id.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="payload">The packet payload.</param>
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
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new PackageException("Packet size exceeds 64KB limit");

        this.Timestamp = timestamp ?? GetMicrosecondTimestamp();
        this.Id = id ?? (byte)(this.Timestamp % byte.MaxValue);
        this.Type = type;
        this.Flags = flags;
        this.Command = command;
        this.Priority = priority;
        (this.Payload, this.IsPooled) = AllocateAndCopyPayload(payload);
        this.Checksum = checksum ?? Crc32.HashToUInt32(payload.Span);
    }

    /// <summary>
    /// Adds new payload to the packet.
    /// </summary>
    /// <param name="payload">The memory payload to be added.</param>
    /// <param name="dispose">Indicates whether to dispose the current packet after creating a new one. Default is true.</param>
    /// <returns>A new Packet with the specified payload.</returns>
    public Packet WithPayload(Memory<byte> payload, bool dispose = true)
    {
        try
        {
            return new Packet(
                this.Id, this.Type, this.Flags,
                this.Priority, this.Command, payload);
        }
        finally
        {
            if (dispose)
                this.Dispose();
        }
    }

    /// <summary>
    /// Adds new payload and flags to the packet.
    /// </summary>
    /// <param name="flags">The byte flags to be added.</param>
    /// <param name="payload">The memory payload to be added.</param>
    /// <param name="dispose">Indicates whether to dispose the current packet after creating a new one. Default is true.</param>
    /// <returns>A new Packet with the specified payload and flags.</returns>
    public Packet WithPayload(byte flags, Memory<byte> payload, bool dispose = true)
    {
        try
        {
            return new Packet(
                this.Id, this.Type, flags.AddFlag((PacketFlags)this.Flags),
                this.Priority, this.Command, payload);
        }
        finally
        {
            if (dispose)
                this.Dispose();
        }
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
        (GetMicrosecondTimestamp() - Timestamp) > (ulong)timeout.TotalMilliseconds;

    /// <summary>
    /// Compares the current packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Packet other)
    {
        if (Type != other.Type || Flags != other.Flags || Command != other.Command || Priority != other.Priority)
            return false;

        return PayloadSpan.SequenceEqual(other.PayloadSpan);
    }

    /// <summary>
    /// Compares the current packet with another object for equality.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the object is a <see cref="Packet"/> and is equal to the current packet; otherwise, false.</returns>
    public override readonly bool Equals(object? obj) => obj is Packet other && Equals(other);

    /// <summary>
    /// Returns a hash code for the current packet.
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
            if (this.Payload.Length <= sizeof(ulong))
            {
                Span<byte> buffer = stackalloc byte[sizeof(ulong)];
                buffer.Clear();
                this.Payload.Span.CopyTo(buffer);
                hash.Add(MemoryMarshal.Read<ulong>(buffer));
            }
            else
            {
                hash.Add(this.Payload.Length);
                hash.AddBytes(this.Payload.Span[..sizeof(ulong)]);
                hash.AddBytes(this.Payload.Span[^sizeof(ulong)..]);
            }
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Releases the resources used by the packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        if (IsPooled && MemoryMarshal.TryGetArray<byte>(Payload, out var segment) && segment.Array is { } array)
            if (array.Length > 1024)
                ArrayPool<byte>.Shared.Return(array);

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public static bool operator ==(Packet left, Packet right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(Packet left, Packet right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetMicrosecondTimestamp() => (ulong)(Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1_000_000L));

    private ReadOnlySpan<byte> PayloadSpan => Payload.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Memory<byte> payload, bool isPooled) AllocateAndCopyPayload(Memory<byte> payload)
    {
        if (payload.IsEmpty) return (Memory<byte>.Empty, false);

        int length = payload.Length;

        if (length <= StackAllocThreshold)
        {
            Span<byte> stackBuffer = stackalloc byte[length]; // Stack allocation for small sizes
            payload.Span.CopyTo(stackBuffer);
            return (stackBuffer.ToArray(), false); // Copy to heap before returning
        }
        else if (length <= HeapAllocThreshold)
        {
            byte[] buffer = GC.AllocateUninitializedArray<byte>(length, pinned: true); // Heap allocation for mid-size
            payload.Span.CopyTo(buffer);
            return (buffer, false);
        }
        else
        {
            var result = ArrayPool<byte>.Shared.Rent(length); // Use ArrayPool for large sizes
            payload.Span.CopyTo(result);
            return (result, true);
        }
    }
}
