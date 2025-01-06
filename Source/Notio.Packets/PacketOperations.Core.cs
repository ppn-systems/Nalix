using Notio.Packets.Extensions;
using Notio.Packets.Metadata;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Packets;

/// <summary>
/// Cung cấp các phương thức mở rộng hiệu suất cao cho lớp Packet.
/// </summary>
[SkipLocalsInit]
public static partial class PacketOperations
{
    private const int MinBufferSize = 256;
    private const int MaxStackAlloc = 512;
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Chuyển đổi Packet thành mảng byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToByteArray(this in Packet packet)
    {
        if (packet.Payload.Length > ushort.MaxValue)
            throw new PacketException("Payload is too large.");

        int totalSize = PacketSize.Header + packet.Payload.Length;

        // Tối ưu cho packets nhỏ bằng stackalloc
        if (totalSize <= MaxStackAlloc)
        {
            Span<byte> stackBuffer = stackalloc byte[totalSize];
            PacketSerializer.WritePacketFast(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }

        // Sử dụng ArrayPool cho packets lớn
        byte[] rentedArray = Pool.Rent(Math.Max(totalSize, MinBufferSize));
        try
        {
            PacketSerializer.WritePacketFast(rentedArray.AsSpan(0, totalSize), in packet);

            if (rentedArray.Length == totalSize)
                return rentedArray;

            return rentedArray.AsSpan(0, totalSize).ToArray();
        }
        catch
        {
            Pool.Return(rentedArray);
            throw;
        }
    }

    /// <summary>
    /// Tạo Packet từ mảng byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet FromByteArray(this ReadOnlySpan<byte> data)
    {
        Debug.Assert(data.Length >= PacketSize.Header, "Data length must be at least header size");

        if (data.Length < PacketSize.Header)
            throw new PacketException("Invalid length.");

        // Kiểm tra length trước khi đọc packet
        short length = MemoryMarshal.Read<short>(data);
        if (length < PacketSize.Header || length > data.Length)
            throw new PacketException("Invalid length.");

        return PacketSerializer.ReadPacketFast(data);
    }
}
