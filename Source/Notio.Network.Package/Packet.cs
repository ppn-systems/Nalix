using Notio.Common.Exceptions;
using Notio.Common.Memory;
using Notio.Network.Package.Models;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Notio.Network.Package;

/// <summary>
/// Represents a packet structure that can be pooled and disposed.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Packet : IEquatable<Packet>, IPoolable, IDisposable
{
    /// <summary>
    /// The minimum size of a packet.
    /// </summary>
    public const ushort MinPacketSize = 256;

    /// <summary>
    /// The maximum size of a packet.
    /// </summary>
    public const ushort MaxPacketSize = ushort.MaxValue;

    private readonly bool _isPooled;
    private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Gets the total length of the packet.
    /// </summary>
    public int Length => (short)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Gets the type of the packet.
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// Gets the flags associated with the packet.
    /// </summary>
    public byte Flags { get; }

    /// <summary>
    /// Gets the priority of the packet.
    /// </summary>
    public byte Priority { get; }

    /// <summary>
    /// Gets the command of the packet.
    /// </summary>
    public short Command { get; }

    /// <summary>
    /// Gets the payload of the packet.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Packet"/> struct.
    /// </summary>
    /// <param name="type">The type of the packet.</param>
    /// <param name="flags">The flags associated with the packet.</param>
    /// <param name="priority">The priority of the packet.</param>
    /// <param name="command">The command of the packet.</param>
    /// <param name="payload">The payload of the packet.</param>
    /// <exception cref="PackageException">Thrown when the packet size exceeds the 64KB limit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(byte type, byte flags, byte priority, short command, ReadOnlyMemory<byte> payload)
    {
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new PackageException("The packet size exceeds the 64KB limit.");

        Type = type;
        Flags = flags;
        Command = command;
        Priority = priority;

        if (payload.Length <= MinPacketSize)
        {
            var inlineArray = new byte[payload.Length];
            payload.Span.CopyTo(inlineArray);
            Payload = inlineArray;
            _isPooled = false;
        }
        else
        {
            var pooledArray = _pool.Rent(payload.Length);
            payload.Span.CopyTo(pooledArray);
            Payload = new ReadOnlyMemory<byte>(pooledArray, 0, payload.Length);
            _isPooled = true;
        }
    }

    /// <summary>
    /// Creates a new packet with a different payload.
    /// </summary>
    /// <param name="newPayload">The new payload for the packet.</param>
    /// <returns>A new packet with the updated payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet WithPayload(ReadOnlyMemory<byte> newPayload) => new(Type, Flags, Priority, Command, newPayload);

    /// <summary>
    /// Resets the packet for pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForPool() => Dispose();

    /// <summary>
    /// Checks if the packet is equal to another packet.
    /// </summary>
    /// <param name="other">The other packet to compare with.</param>
    /// <returns>True if the packets are equal, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Packet other)
    {
        if (Type != other.Type || Flags != other.Flags || Command != other.Command || Priority != other.Priority)
            return false;

        if (Payload.Length != other.Payload.Length)
            return false;

        if (Payload.Length == 0)
            return true;

        // Optimize comparison for small payloads with zero-padding
        if (Payload.Length <= sizeof(ulong))
        {
            Span<byte> buffer1 = stackalloc byte[sizeof(ulong)];
            Span<byte> buffer2 = stackalloc byte[sizeof(ulong)];
            buffer1.Clear();
            buffer2.Clear();

            Payload.Span.CopyTo(buffer1);
            other.Payload.Span.CopyTo(buffer2);

            return MemoryMarshal.Read<ulong>(buffer1) == MemoryMarshal.Read<ulong>(buffer2);
        }

        // Use SIMD for larger payloads
        if (Vector128.IsHardwareAccelerated)
            return MemoryCompareVectorized(Payload.Span, other.Payload.Span);

        return Payload.Span.SequenceEqual(other.Payload.Span);
    }

    /// <summary>
    /// Checks if the packet is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the object is a packet and is equal to the current packet, otherwise false.</returns>
    public override bool Equals(object? obj) => obj is Packet other && Equals(other);

    /// <summary>
    /// Gets the hash code for the packet.
    /// </summary>
    /// <returns>The hash code for the packet.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Flags);
        hash.Add(Command);
        hash.Add(Priority);

        if (Payload.Length > 0)
        {
            if (Payload.Length <= sizeof(ulong))
            {
                Span<byte> buffer = stackalloc byte[sizeof(ulong)];
                buffer.Clear();
                Payload.Span.CopyTo(buffer);
                hash.Add(MemoryMarshal.Read<ulong>(buffer));
            }
            else
            {
                hash.Add(Payload.Length);
                hash.AddBytes(Payload.Span[..sizeof(ulong)]);
                hash.AddBytes(Payload.Span[^sizeof(ulong)..]);
            }
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Equality operator for packets.
    /// </summary>
    /// <param name="left">The left packet.</param>
    /// <param name="right">The right packet.</param>
    /// <returns>True if the packets are equal, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Packet left, Packet right) => left.Equals(right);

    /// <summary>
    /// Inequality operator for packets.
    /// </summary>
    /// <param name="left">The left packet.</param>
    /// <param name="right">The right packet.</param>
    /// <returns>True if the packets are not equal, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Packet left, Packet right) => !(left == right);

    /// <summary>
    /// Implicit conversion to ReadOnlyMemory&lt;byte&gt;.
    /// </summary>
    /// <param name="packet">The packet to convert.</param>
    /// <returns>The payload of the packet as ReadOnlyMemory&lt;byte&gt;.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyMemory<byte>(Packet packet) => packet.Payload;

    /// <summary>
    /// Disposes the packet and returns it to the pool if it was pooled.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_isPooled && MemoryMarshal.TryGetArray(Payload, out var segment) && segment.Array != null)
        {
            _pool.Return(segment.Array);
        }
    }

    /// <summary>
    /// Compares memory using SIMD for larger payloads.
    /// </summary>
    /// <param name="first">The first memory span to compare.</param>
    /// <param name="second">The second memory span to compare.</param>
    /// <returns>True if the memory spans are equal, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MemoryCompareVectorized(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        Debug.Assert(first.Length == second.Length);

        if (first.Length < Vector128<byte>.Count)
            return first.SequenceEqual(second);

        int offset = 0;
        int length = first.Length;

        // Xử lý phần dư đầu tiên không đủ 16 bytes
        if ((length % Vector128<byte>.Count) != 0)
        {
            int remainder = length % Vector128<byte>.Count;
            if (!first[^remainder..].SequenceEqual(second[^remainder..]))
                return false;

            length -= remainder;
        }

        // So sánh chính bằng SIMD
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
}