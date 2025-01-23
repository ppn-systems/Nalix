using Notio.Common.Exceptions;
using Notio.Common.Memory;
using Notio.Packets.Metadata;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Notio.Packets;

/// <summary>
/// Đại diện cho một Packet với hiệu suất cao, tối ưu bộ nhớ.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Packet : IEquatable<Packet>, IPoolable, IDisposable
{
    public const ushort MinPacketSize = 256;
    public const ushort MaxPacketSize = ushort.MaxValue;

    private readonly bool _isPooled;
    private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Lấy tổng chiều dài của gói tin bao gồm tiêu đề và tải trọng.
    /// </summary>
    public int Length => (short)(PacketSize.Header + Payload.Length);

    /// <summary>
    /// Lấy loại của gói tin.
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// Lấy các cờ liên quan đến gói tin.
    /// </summary>
    public byte Flags { get; }

    /// <summary>
    /// Lấy lệnh liên quan đến gói tin.
    /// </summary>
    public short Command { get; }

    /// <summary>
    /// Lấy tải trọng của gói tin.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Khởi tạo một thể hiện mới của cấu trúc <see cref="Packet"/>.
    /// </summary>
    /// <param name="type">Loại của gói tin.</param>
    /// <param name="flags">Các cờ liên quan đến gói tin.</param>
    /// <param name="command">Lệnh liên quan đến gói tin.</param>
    /// <param name="payload">Tải trọng của gói tin.</param>
    /// <exception cref="PacketException">Ném ra nếu kích thước gói tin vượt quá giới hạn 64KB.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet(byte type, byte flags, short command, ReadOnlyMemory<byte> payload)
    {
        if (payload.Length + PacketSize.Header > MaxPacketSize)
            throw new PacketException("The packet size exceeds the 64KB limit.");

        Type = type;
        Flags = flags;
        Command = command;

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
    /// Tạo một gói mới với tải trọng được chỉ định, giữ lại loại, cờ và lệnh.
    /// </summary>
    /// <param name="newPayload">Tải trọng mới cho gói tin.</param>
    /// <returns>Một gói mới với tải trọng cập nhật.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Packet WithPayload(ReadOnlyMemory<byte> newPayload) =>
        new(Type, Flags, Command, newPayload);

    /// <summary>
    /// Đặt lại gói tin, chuẩn bị nó cho việc tái sử dụng bởi bộ nhớ đệm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForPool() => Dispose();

    /// <summary>
    /// Xác định liệu gói tin được chỉ định có bằng gói tin hiện tại không.
    /// Sử dụng các kỹ thuật so sánh bộ nhớ tối ưu, bao gồm SIMD cho các tải trọng lớn hơn.
    /// </summary>
    /// <param name="other">Gói tin để so sánh với gói tin hiện tại.</param>
    /// <returns>true nếu gói tin được chỉ định bằng gói tin hiện tại; ngược lại là false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Packet other)
    {
        if (Type != other.Type || Flags != other.Flags || Command != other.Command)
            return false;

        if (Payload.Length != other.Payload.Length)
            return false;

        // Xử lý tối ưu cho tải trọng nhỏ
        if (Payload.Length > 0 && Payload.Length <= sizeof(ulong))
        {
            ulong payload1 = 0;
            ulong payload2 = 0;

            // Đảm bảo chỉ đọc dữ liệu trong phạm vi an toàn
            Span<byte> buffer1 = stackalloc byte[sizeof(ulong)];
            Span<byte> buffer2 = stackalloc byte[sizeof(ulong)];

            Payload.Span.CopyTo(buffer1);
            other.Payload.Span.CopyTo(buffer2);

            payload1 = MemoryMarshal.Read<ulong>(buffer1);
            payload2 = MemoryMarshal.Read<ulong>(buffer2);

            return payload1 == payload2;
        }

        // Xử lý SIMD cho tải trọng lớn hơn
        if (Vector128.IsHardwareAccelerated && Payload.Length >= Vector128<byte>.Count)
            return MemoryCompareVectorized(Payload.Span, other.Payload.Span);

        return Payload.Span.SequenceEqual(other.Payload.Span);
    }

    public override bool Equals(object? obj)
        => obj is Packet other && Equals(other);

    /// <summary>
    /// Phục vụ như là hàm băm mặc định, cung cấp một mã băm duy nhất cho gói tin.
    /// </summary>
    /// <returns>Mã băm cho gói tin hiện tại.</returns>
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
    private static bool MemoryCompareVectorized(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        Debug.Assert(first.Length == second.Length);

        int i = 0;
        int length = first.Length;

        // So sánh 16 bytes mỗi lần using SIMD
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

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi gói tin.
    /// Nếu tải trọng là từ bộ nhớ đệm, trả lại bộ nhớ cho bộ nhớ đệm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_isPooled && Payload.Length > 0)
        {
            if (MemoryMarshal.TryGetArray(Payload, out var segment) && segment.Array != null)
                _pool.Return(segment.Array);
        }

        GC.SuppressFinalize(this);
    }
}