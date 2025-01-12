using Notio.Common.Memory;
using Notio.Packets.Exceptions;
using Notio.Packets.Metadata;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Notio.Packets;

/// <summary>
/// Đại diện cho một packet dữ liệu với hiệu suất cao và tối ưu memory.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Packet : IEquatable<Packet>, IPoolable, IDisposable
{
    public const int MaxPacketSize = ushort.MaxValue;
    private const int MaxInlinePayloadSize = 128;
    private static readonly ArrayPool<byte> SharedPool = ArrayPool<byte>.Shared;

    public int Length
    {
        get
        {
            return (short)(PacketSize.Header + Payload.Length);
        }
    }

    public byte Type { get; }
    public byte Flags { get; }
    public short Command { get; }
    public ReadOnlyMemory<byte> Payload { get; }

    private readonly bool _isPooled;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(byte type, byte flags, short command, ReadOnlyMemory<byte> payload)
    {
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new PacketException("Packet size exceeds the 64KB limit.");

        Type = type;
        Flags = flags;
        Command = command;

        if (payload.Length <= MaxInlinePayloadSize)
        {
            var inlineArray = new byte[payload.Length];
            payload.Span.CopyTo(inlineArray);
            Payload = inlineArray;
            _isPooled = false;
        }
        else
        {
            var pooledArray = SharedPool.Rent(payload.Length);
            payload.Span.CopyTo(pooledArray);
            Payload = new ReadOnlyMemory<byte>(pooledArray, 0, payload.Length);
            _isPooled = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_isPooled && Payload.Length > 0)
        {
            if (MemoryMarshal.TryGetArray(Payload, out var segment) && segment.Array != null)
                SharedPool.Return(segment.Array);
        }

        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForPool() => Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Packet other)
    {
        if (Type != other.Type || Flags != other.Flags || Command != other.Command)
            return false;

        // Tối ưu cho payload nhỏ
        if (Payload.Length != other.Payload.Length)
            return false;

        if (Payload.Length <= sizeof(ulong))
            return MemoryMarshal.Read<ulong>(Payload.Span) ==
                   MemoryMarshal.Read<ulong>(other.Payload.Span);

        // SIMD comparison cho payload lớn
        if (Vector128.IsHardwareAccelerated && Payload.Length >= Vector128<byte>.Count)
            return MemoryCompareVectorized(Payload.Span, other.Payload.Span);

        return Payload.Span.SequenceEqual(other.Payload.Span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MemoryCompareVectorized(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        Debug.Assert(first.Length == second.Length);

        int i = 0;
        int length = first.Length;

        // So sánh 16 bytes một lần using SIMD
        while (length >= Vector128<byte>.Count)
        {
            Vector128<byte> v1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(first[i..]));
            Vector128<byte> v2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(second[i..]));

            if (!Vector128.EqualsAll(v1, v2))
                return false;

            i += Vector128<byte>.Count;
            length -= Vector128<byte>.Count;
        }

        // So sánh bytes còn lại
        return first[i..].SequenceEqual(second[i..]);
    }

    public override bool Equals(object? obj)
        => obj is Packet other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Flags);
        hash.Add(Command);

        if (Payload.Length <= sizeof(ulong))
        {
            hash.Add(MemoryMarshal.Read<ulong>(Payload.Span));
        }
        else
        {
            hash.Add(Payload.Length);
            hash.Add(MemoryMarshal.Read<ulong>(Payload.Span));
            if (Payload.Length > sizeof(ulong))
            {
                hash.Add(MemoryMarshal.Read<ulong>(
                    Payload.Span[(Payload.Length - sizeof(ulong))..]));
            }
        }

        return hash.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Packet left, Packet right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Packet left, Packet right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyMemory<byte>(Packet packet) => packet.Payload;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet WithPayload(ReadOnlyMemory<byte> newPayload) =>
        new(Type, Flags, Command, newPayload);
}