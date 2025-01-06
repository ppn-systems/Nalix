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
public static partial class PacketOperations
{
    /// <summary>
    /// Kiểm tra tính hợp lệ của Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(this in Packet packet)
    {
        return packet.Payload.Length <= ushort.MaxValue &&
               packet.Payload.Length + PacketSize.Header <= ushort.MaxValue;
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
}