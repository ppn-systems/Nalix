using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Notio.Packets.Utilities;

public static partial class PacketOperations
{
    /// <summary>
    /// Tạo một bản sao độc lập của Packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet Clone(this in Packet packet)
    {
        // Sao chép payload với sự kiểm tra an toàn
        byte[] payloadCopy = new byte[packet.Payload.Length];
        packet.Payload.Span.CopyTo(payloadCopy);

        return new Packet(packet.Type, packet.Flags, packet.Priority, packet.Command, payloadCopy);
    }

    /// <summary>
    /// Thử sao chép packet mà không gây lỗi, trả về thành công hay thất bại.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryClone(this in Packet packet, out Packet clonedPacket)
    {
        try
        {
            clonedPacket = packet.Clone();
            return true;
        }
        catch
        {
            clonedPacket = default;
            return false;
        }
    }

    /// <summary>
    /// Tạo bản sao với các cải tiến hiệu suất.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Packet CloneOptimized(this in Packet packet)
    {
        // Tối ưu hóa sao chép Payload cho trường hợp không cần tạo mảng mới
        if (packet.Payload.Length <= Packet.MinPacketSize)
            return packet;

        byte[] payloadCopy = ArrayPool<byte>.Shared.Rent(packet.Payload.Length);
        packet.Payload.Span.CopyTo(payloadCopy);
        Packet newPacket = new(packet.Type, packet.Flags, packet.Priority, packet.Command,
            new ReadOnlyMemory<byte>(payloadCopy, 0, packet.Payload.Length));

        return newPacket;
    }
}