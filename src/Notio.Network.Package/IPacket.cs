using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Cryptography.Hash;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Metadata;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Notio.Network.Package;

/// <summary>
/// Represents a packet structure that can be pooled and disposed.
/// This packet contains metadata and a payload with associated checksum and flags.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Packet : IPacket
{
    /// <summary>
    /// The minimum packet size (in bytes).
    /// </summary>
    public const ushort MinPacketSize = 256;

    /// <summary>
    /// Threshold size for deciding whether to use stack allocation or heap allocation for payload.
    /// </summary>
    public const ushort StackAllocThreshold = 128;

    /// <summary>
    /// Threshold for optimized vectorized memory comparison.
    /// </summary>
    public const ushort VectorCompareThreshold = 32;

    /// <summary>
    /// The maximum allowed packet size (in bytes), 64KB.
    /// </summary>
    public const ushort MaxPacketSize = ushort.MaxValue;

    private Memory<byte> _payload;
    private uint _checksum;
    private byte _flags;

    private bool IsPooled;

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
    public byte Flags
    {
        readonly get => _flags;
        private set => _flags = value;
    }

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
    public uint Checksum
    {
        readonly get => _checksum;
        private set => _checksum = value;
    }

    /// <summary>
    /// Gets the payload of the packet.
    /// </summary>
    public Memory<byte> Payload
    {
        readonly get => _payload;
        private set => _payload = value;
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
    {
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new PackageException("The packet size exceeds the 64KB limit.");

        this.Timestamp = (ulong)(DateTime.UtcNow.Ticks / 10);
        this.Id = (byte)(Timestamp % byte.MaxValue);
        this.Type = type;
        this.Flags = flags;
        this.Command = command;
        this.Priority = priority;
        this.Checksum = Crc32.ComputeChecksum(payload.Span);
        (this.Payload, this.IsPooled) = AllocatePayload(payload);
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
    {
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new PackageException("The packet size exceeds the 64KB limit.");

        this.Timestamp = (ulong)(DateTime.UtcNow.Ticks / 10);
        this.Id = (byte)(Timestamp % byte.MaxValue);
        this.Type = (byte)type;
        this.Flags = (byte)flags;
        this.Command = command;
        this.Priority = (byte)priority;
        this.Checksum = Crc32.ComputeChecksum(payload.Span);
        (this.Payload, this.IsPooled) = AllocatePayload(payload);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IPacket"/> struct.
    /// </summary>
    /// <param name="id">The packet identifier.</param>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The packet flags.</param>
    /// <param name="priority">The packet priority.</param>
    /// <param name="command">The packet command.</param>
    /// <param name="timestamp">The packet timestamp.</param>
    /// <param name="checksum">The packet checksum.</param>
    /// <param name="payload">The packet payload.</param>
    /// <exception cref="PackageException">Thrown if the payload size exceeds the maximum allowed packet size.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(byte id, byte type, byte flags, byte priority, ushort command, ulong timestamp, uint checksum, Memory<byte> payload)
    {
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new PackageException("The packet size exceeds the 64KB limit.");

        this.Id = id;
        this.Type = type;
        this.Flags = flags;
        this.Priority = priority;
        this.Command = command;
        this.Timestamp = timestamp;
        this.Checksum = checksum;
        (this.Payload, this.IsPooled) = AllocatePayload(payload);
    }

    /// <summary>
    /// Verifies the packet's checksum matches the computed checksum based on the payload.
    /// </summary>
    /// <returns>True if the checksum matches; otherwise, false.</returns>
    public readonly bool IsValid() => Crc32.ComputeChecksum(this.Payload.Span) == this.Checksum;

    /// <summary>
    /// Determines if the packet has expired based on the provided timeout.
    /// </summary>
    /// <param name="timeout">The timeout to compare against the packet's timestamp.</param>
    /// <returns>True if the packet has expired; otherwise, false.</returns>
    public readonly bool IsExpired(TimeSpan timeout) =>
        (ulong)(DateTime.UtcNow.Ticks / 10) - Timestamp > timeout.TotalMilliseconds;

    /// <summary>
    /// Implicit conversion from <see cref="Packet"/> to <see cref="Memory{Byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Memory<byte>(Packet packet) => packet.Payload;

    /// <summary>
    /// Returns a <see cref="ReadOnlyMemory{Byte}"/> representation of the packet payload.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlyMemory<byte> AsReadOnlyMemory() => this.Payload;

    /// <summary>
    /// Returns a mutable <see cref="Memory{Byte}"/> representation of the packet payload.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Memory<byte> GetMutablePayload() => this.Payload;

    /// <summary>
    /// Updates the current packet's payload without creating a new instance.
    /// </summary>
    /// <param name="newPayload">The new payload to replace the current one.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdatePayload(Memory<byte> newPayload)
    {
        if (newPayload.Length + PacketSize.Header > MaxPacketSize)
            throw new PackageException("The packet size exceeds the 64KB limit.");

        // Only return the previous payload if it was pooled.
        if (this.IsPooled && MemoryMarshal.TryGetArray<byte>(this.Payload, out var segment) && segment.Array != null)
        {
            try { ArrayPool<byte>.Shared.Return(segment.Array); }
            catch (ObjectDisposedException) { }
        }

        this.Checksum = Crc32.ComputeChecksum(newPayload.Span);
        (this.Payload, this.IsPooled) = AllocatePayload(newPayload);
    }

    /// <summary>
    /// Updates the flags of the current packet.
    /// </summary>
    /// <param name="flags">The new flags to set.</param>
    public void UpdateFlags(byte flags) => this.Flags = flags;

    /// <summary>
    /// Compares the current packet with another packet for equality.
    /// </summary>
    /// <param name="other">The packet to compare with.</param>
    /// <returns>True if the packets are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(IPacket? other)
    {
        if (other is null) return false;
        if (this.Type != other.Type ||
            this.Flags != other.Flags ||
            this.Command != other.Command ||
            this.Priority != other.Priority)
            return false;

        if (this.Payload.Length != other.Payload.Length)
            return false;

        if (this.Payload.Length == 0)
            return true;

        if (Payload.Length <= sizeof(ulong))
        {
            Span<byte> buffer1 = stackalloc byte[sizeof(ulong)];
            Span<byte> buffer2 = stackalloc byte[sizeof(ulong)];
            buffer1.Clear();
            buffer2.Clear();

            this.Payload.Span.CopyTo(buffer1);
            other.Payload.Span.CopyTo(buffer2);

            return MemoryMarshal.Read<ulong>(buffer1) == MemoryMarshal.Read<ulong>(buffer2);
        }

        if (Vector128.IsHardwareAccelerated)
            return MemoryCompareVectorized(this.Payload.Span, other.Payload.Span);

        return this.Payload.Span.SequenceEqual(other.Payload.Span);
    }

    /// <summary>
    /// Compares the current packet with another object for equality.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the object is a <see cref="IPacket"/> and is equal to the current packet; otherwise, false.</returns>
    public override readonly bool Equals(object? obj) => obj is IPacket other && Equals(other);

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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Packet left, Packet right) => left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Packet left, Packet right) => !(left == right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyMemory<byte>(Packet packet) => packet.Payload;

    /// <summary>
    /// Releases the resources used by the packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        if (!this.IsPooled) return; // Only return if the memory was rented from ArrayPool.
        if (MemoryMarshal.TryGetArray<byte>(this.Payload, out var segment) && segment.Array != null)
        {
            try { ArrayPool<byte>.Shared.Return(segment.Array); }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>
    /// Compares two memory spans using vectorized operations.
    /// </summary>
    /// <param name="first">The first memory span.</param>
    /// <param name="second">The second memory span.</param>
    /// <returns>true if the spans are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MemoryCompareVectorized(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        Debug.Assert(first.Length == second.Length);

        if (first.Length < Vector128<byte>.Count)
            return first.SequenceEqual(second);

        int offset = 0;
        int length = first.Length;

        // Compare the trailing bytes that do not fit into a full vector.
        if ((length % Vector128<byte>.Count) != 0)
        {
            int remainder = length % Vector128<byte>.Count;
            if (!first[^remainder..].SequenceEqual(second[^remainder..]))
                return false;

            length -= remainder;
        }

        // Compare using SIMD.
        while (length > 0)
        {
            var v1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(first), (nuint)offset);
            var v2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(second), (nuint)offset);

            if (v1 != v2)
                return false;

            offset += Vector128<byte>.Count;
            length -= Vector128<byte>.Count;
        }

        return true;
    }

    /// <summary>
    /// Allocates the payload memory.
    /// </summary>
    /// <param name="payload">The payload to allocate.</param>
    /// <returns>A tuple containing the allocated memory and a boolean indicating whether the memory is pooled.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Memory<byte> Memory, bool IsPooled) AllocatePayload(Memory<byte> payload)
    {
        int length = payload.Length;
        if (length == 0) return (Memory<byte>.Empty, false);

        if (length <= StackAllocThreshold)
        {
            Span<byte> stackBuffer = stackalloc byte[length];
            payload.Span.CopyTo(stackBuffer);
            return (stackBuffer.ToArray(), false);
        }

        if (length <= MinPacketSize)
        {
            byte[] inlineArray = GC.AllocateUninitializedArray<byte>(length);
            payload.Span.CopyTo(inlineArray);
            return (inlineArray, false);
        }

        byte[] pooledArray = ArrayPool<byte>.Shared.Rent(length);
        payload.Span.CopyTo(pooledArray);
        return (new Memory<byte>(pooledArray, 0, length), true);
    }
}
