using Notio.Packets.Extensions;
using Notio.Packets.Helpers;
using Notio.Packets.Metadata;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Packets;

/// <summary> 
/// Cung cấp các phương thức mở rộng cho lớp Packet.
/// </summary> 
[SkipLocalsInit]
public static class PacketOperations
{
    private const int MinBufferSize = 256;
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Chuyển đổi Packet thành mảng byte.
    /// </summary>
    /// <param name="packet">Đối tượng Packet.</param>
    /// <returns>Mảng byte tương ứng.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToByteArray(this in Packet packet)
    {
        if (packet.Payload.Length > ushort.MaxValue)
            ThrowHelper.ThrowPayloadTooLarge();

        int totalSize = PacketSize.Header + packet.Payload.Length;
        byte[] rentedArray = Pool.Rent(Math.Max(totalSize, MinBufferSize));

        try
        {
            PacketSerializer.WritePacketFast(rentedArray.AsSpan(0, totalSize), in packet);
            return rentedArray.AsSpan(0, totalSize).ToArray();
        }
        finally
        {
            Pool.Return(rentedArray);
        }
    }

    /// <summary>
    /// Tạo Packet từ mảng byte.
    /// </summary>
    /// <param name="data">Mảng byte.</param>
    /// <returns>Đối tượng Packet tương ứng.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet FromByteArray(this ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            ThrowHelper.ThrowInvalidPacketSize();

        return PacketSerializer.ReadPacketFast(data);
    }

    /// <summary>
    /// Thử chuyển đổi Packet thành mảng byte.
    /// </summary>
    /// <param name="packet">Đối tượng Packet.</param>
    /// <param name="destination">Mảng byte đích.</param>
    /// <param name="bytesWritten">Số byte đã ghi.</param>
    /// <returns>True nếu thành công, ngược lại là False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToByteArray(this in Packet packet, Span<byte> destination, out int bytesWritten)
    {
        if (packet.Payload.Length > ushort.MaxValue)
        {
            bytesWritten = 0;
            return false;
        }

        int totalSize = PacketSize.Header + packet.Payload.Length;
        if (destination.Length < totalSize)
        {
            bytesWritten = 0;
            return false;
        }

        PacketSerializer.WritePacketFast(destination[..totalSize], in packet);
        bytesWritten = totalSize;
        return true;
    }

    /// <summary>
    /// Thử tạo Packet từ mảng byte.
    /// </summary>
    /// <param name="source">Mảng byte nguồn.</param>
    /// <param name="packet">Đối tượng Packet.</param>
    /// <returns>True nếu thành công, ngược lại là False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFromByteArray(ReadOnlySpan<byte> source, out Packet packet)
    {
        if (source.Length < PacketSize.Header)
        {
            packet = default;
            return false;
        }

        short length = MemoryMarshal.Read<short>(source);
        if (length < PacketSize.Header || length > source.Length)
        {
            packet = default;
            return false;
        }

        packet = PacketSerializer.ReadPacketFast(source);
        return true;
    }
}