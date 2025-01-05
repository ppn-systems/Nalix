using Notio.Packets.Extensions;
using Notio.Packets.Helpers;
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
public static class PacketOperations
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
            ThrowHelper.ThrowPayloadTooLarge();

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
            ThrowHelper.ThrowInvalidPacketSize();

        // Kiểm tra length trước khi đọc packet
        short length = MemoryMarshal.Read<short>(data);
        if (length < PacketSize.Header || length > data.Length)
            ThrowHelper.ThrowInvalidLength();

        return PacketSerializer.ReadPacketFast(data);
    }

    /// <summary>
    /// Thử chuyển đổi Packet thành mảng byte.
    /// </summary>
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

        try
        {
            PacketSerializer.WritePacketFast(destination[..totalSize], in packet);
            bytesWritten = totalSize;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            bytesWritten = 0;
            return false;
        }
    }

    /// <summary>
    /// Thử tạo Packet từ mảng byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFromByteArray(ReadOnlySpan<byte> source, out Packet packet)
    {
        if (source.Length < PacketSize.Header)
        {
            packet = default;
            return false;
        }

        try
        {
            // Validate packet length
            short length = MemoryMarshal.Read<short>(source);
            if (length < PacketSize.Header || length > source.Length)
            {
                packet = default;
                return false;
            }

            // Validate payload size
            int payloadSize = length - PacketSize.Header;
            if (payloadSize > ushort.MaxValue)
            {
                packet = default;
                return false;
            }

            packet = PacketSerializer.ReadPacketFast(source[..length]);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            packet = default;
            return false;
        }
    }

    /// <summary>
    /// Tạo một bản sao độc lập của Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Clone(this in Packet packet)
    {
        var payloadCopy = packet.Payload.ToArray();
        return new Packet(packet.Type, packet.Flags, packet.Command, payloadCopy);
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(this in Packet packet)
    {
        return packet.Payload.Length <= ushort.MaxValue &&
               packet.Payload.Length + PacketSize.Header <= ushort.MaxValue;
    }
}