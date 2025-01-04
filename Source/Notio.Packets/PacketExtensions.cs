using Notio.Packets.Metadata;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Packets;

/// <summary> 
/// Cung cấp các phương thức mở rộng cho lớp Packet.
/// <summary> 
[SkipLocalsInit]
public static class PacketExtensions
{
    private const int MinBufferSize = 256;

    /// <summary>
    /// Chuyển đổi Packet thành mảng byte.
    /// </summary>
    /// <param name="packet">Đối tượng Packet.</param>
    /// <returns>Mảng byte tương ứng.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ToByteArray(this Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet.Payload);

        if ((uint)packet.Payload.Length > ushort.MaxValue)
            ThrowPayloadTooLarge();

        int totalSize = PacketSize.Header + packet.Payload.Length;
        byte[] rentedArray = ArrayPool<byte>.Shared.Rent(Math.Max(totalSize, MinBufferSize));

        try
        {
            Span<byte> buffer = rentedArray.AsSpan(0, totalSize);

            // Fast path: write all header data in one go if possible
            if (RuntimeHelpers.IsReferenceOrContainsReferences<Packet>())
            {
                WriteHeaderSlow(buffer, packet, totalSize);
            }
            else
            {
                ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                Unsafe.WriteUnaligned(ref bufferRef, (short)totalSize);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferRef, PacketOffset.Type), packet.Type);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferRef, PacketOffset.Flags), packet.Flags);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferRef, PacketOffset.Command), packet.Command);
            }

            // Copy payload 
            packet.Payload.CopyTo(buffer[PacketSize.Header..]);

            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedArray);
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
            ThrowInvalidPacketSize();

        // Fast path: read all header data in one go if possible
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<Packet>())
        {
            ref byte dataRef = ref MemoryMarshal.GetReference(data);
            short length = Unsafe.ReadUnaligned<short>(ref dataRef);

            if ((uint)length > data.Length)
                ThrowInvalidLength();

            return new Packet
            {
                Type = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref dataRef, PacketOffset.Type)),
                Flags = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref dataRef, PacketOffset.Flags)),
                Command = Unsafe.ReadUnaligned<short>(ref Unsafe.Add(ref dataRef, PacketOffset.Command)),
                Payload = data[PacketSize.Header..length].ToArray()
            };
        }

        return ReadPacketSlow(data);
    }

    /// <summary>
    /// Thử chuyển đổi Packet thành mảng byte.
    /// </summary>
    /// <param name="packet">Đối tượng Packet.</param>
    /// <param name="destination">Mảng byte đích.</param>
    /// <param name="bytesWritten">Số byte đã ghi.</param>
    /// <returns>True nếu thành công, ngược lại là False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToByteArray(this Packet packet, Span<byte> destination, out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(packet.Payload);

        if (packet.Payload.Length > ushort.MaxValue)
        {
            bytesWritten = 0;
            return false;
        }

        short totalSize = (short)(PacketSize.Header + packet.Payload.Length);

        if (destination.Length < totalSize)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteInt16LittleEndian(destination[..PacketSize.Length], totalSize);
        destination[PacketOffset.Type] = packet.Type;
        destination[PacketOffset.Flags] = packet.Flags;
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(PacketOffset.Command, sizeof(short)), packet.Command);

        packet.Payload.CopyTo(destination[PacketSize.Header..]);

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

        short length = BinaryPrimitives.ReadInt16LittleEndian(source);

        if (length < PacketSize.Header || length > source.Length)
        {
            packet = default;
            return false;
        }

        packet = new Packet
        {
            Type = source[PacketOffset.Type],
            Flags = source[PacketOffset.Flags],
            Command = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(PacketOffset.Command, sizeof(short))),
            Payload = source[PacketSize.Header..length].ToArray()
        };

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteHeaderSlow(Span<byte> buffer, in Packet packet, int totalSize)
    {
        BinaryPrimitives.WriteInt16LittleEndian(buffer, (short)totalSize);
        buffer[PacketOffset.Type] = packet.Type;
        buffer[PacketOffset.Flags] = packet.Flags;
        BinaryPrimitives.WriteInt16LittleEndian(buffer[PacketOffset.Command..], packet.Command);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Packet ReadPacketSlow(ReadOnlySpan<byte> data)
    {
        short length = BinaryPrimitives.ReadInt16LittleEndian(data);

        if ((uint)length > data.Length)
            ThrowInvalidLength();

        return new Packet
        {
            Type = data[PacketOffset.Type],
            Flags = data[PacketOffset.Flags],
            Command = BinaryPrimitives.ReadInt16LittleEndian(data[PacketOffset.Command..]),
            Payload = data[PacketSize.Header..length].ToArray()
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowPayloadTooLarge() =>
        throw new ArgumentException("Payload size exceeds maximum allowed");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidPacketSize() =>
        throw new ArgumentException("Data too short for packet header");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidLength() =>
        throw new ArgumentException("Invalid packet length");
}