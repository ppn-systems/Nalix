using Notio.Common.Exceptions;
using Notio.Network.Package.Models;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Serialization.Unsafes;

[SkipLocalsInit]
internal static unsafe class PacketSerializerUnsafe
{
    private const int MaxStackAllocSize = 1024; // Giới hạn kích thước stack alloc

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WritePacketUnsafe(byte* destination, in Packet packet)
    {
        if (destination == null)
            throw new PackageException("Destination pointer is null.");

        if ((uint)packet.Payload.Length > ushort.MaxValue)
            throw new PackageException("Payload size exceeds maximum allowed length.");

        int totalSize = PacketSize.Header + packet.Payload.Length;

        // Sử dụng Span để tối ưu hóa việc ghi header
        Span<byte> headerSpan = new(destination, PacketSize.Header);
        BinaryPrimitives.WriteInt16LittleEndian(headerSpan, (short)totalSize);
        headerSpan[PacketOffset.Type] = packet.Type;
        headerSpan[PacketOffset.Flags] = packet.Flags;
        BinaryPrimitives.WriteInt16LittleEndian(headerSpan[PacketOffset.Command..], packet.Command);

        // Copy payload với kiểm tra bounds
        if (!packet.Payload.IsEmpty)
        {
            fixed (byte* payloadPtr = packet.Payload.Span)
            {
                Buffer.MemoryCopy(
                    payloadPtr,
                    destination + PacketSize.Header,
                    packet.Payload.Length,
                    packet.Payload.Length
                );
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReadPacketUnsafe(byte* source, out Packet packet, int length)
    {
        if (source == null || length < PacketSize.Header)
            throw new PackageException("Invalid packet source or insufficient length.");

        // Đọc header
        Span<byte> headerSpan = new(source, PacketSize.Header);
        short packetLength = BinaryPrimitives.ReadInt16LittleEndian(headerSpan);

        if ((uint)packetLength > length)
            throw new PackageException("Packet size exceeds the provided buffer length.");

        // Tính toán độ dài payload
        int payloadLength = packetLength - PacketSize.Header;

        // Đọc payload
        ReadOnlyMemory<byte> payloadMemory = ReadPayload(source + PacketSize.Header, payloadLength);

        // Khởi tạo packet
        packet = new Packet
        (
            type: headerSpan[PacketOffset.Type],
            flags: headerSpan[PacketOffset.Flags],
            priority: headerSpan[PacketOffset.Priority],
            command: BinaryPrimitives.ReadInt16LittleEndian(headerSpan[PacketOffset.Command..]),
            payload: payloadMemory
        );
    }

    private static ReadOnlyMemory<byte> ReadPayload(byte* payloadSource, int payloadLength)
    {
        if (payloadLength <= MaxStackAllocSize)
        {
            // Dùng stackalloc nếu payload nhỏ
            Span<byte> stackBuffer = stackalloc byte[payloadLength];
            fixed (byte* stackPtr = stackBuffer)
            {
                Buffer.MemoryCopy(payloadSource, stackPtr, payloadLength, payloadLength);
            }
            return stackBuffer.ToArray();
        }
        else
        {
            // Sử dụng ArrayPool nếu payload lớn
            byte[] rentedArray = ArrayPool<byte>.Shared.Rent(payloadLength);
            try
            {
                fixed (byte* rentedPtr = rentedArray)
                {
                    Buffer.MemoryCopy(payloadSource, rentedPtr, payloadLength, payloadLength);
                }
                return new ReadOnlyMemory<byte>(rentedArray, 0, payloadLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }
}