using Notio.Packets.Helpers;
using Notio.Packets.Metadata;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Packets.Extensions;

[SkipLocalsInit]
internal static unsafe class PacketSerializerUnsafe
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WritePacketUnsafe(byte* destination, in Packet packet)
    {
        if ((uint)packet.Payload.Length > ushort.MaxValue)
            ThrowHelper.ThrowPayloadTooLarge();

        int totalSize = PacketSize.Header + packet.Payload.Length;

        // Write header fields directly using pointer arithmetic
        *(short*)destination = (short)totalSize;
        destination[PacketOffset.Type] = packet.Type;
        destination[PacketOffset.Flags] = packet.Flags;
        *(short*)(destination + PacketOffset.Command) = packet.Command;

        // Copy payload using direct memory copy
        fixed (byte* payloadPtr = packet.Payload.Span)
        {
            Buffer.MemoryCopy(
                payloadPtr,                    // source
                destination + PacketSize.Header, // destination 
                packet.Payload.Length,         // destinationSizeInBytes
                packet.Payload.Length          // sourceBytesToCopy
            );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe void ReadPacketUnsafe(byte* source, out Packet packet, int length)
    {
        if (length < PacketSize.Header)
            ThrowHelper.ThrowInvalidPacketSize();

        short packetLength = *(short*)source;
        if ((uint)packetLength > length)
            ThrowHelper.ThrowInvalidLength();

        // Khởi tạo một instance mới của Packet với các giá trị mặc định
        packet = new Packet
        {
            Type = source[PacketOffset.Type],
            Flags = source[PacketOffset.Flags],
            Command = *(short*)(source + PacketOffset.Command)
        };

        // Copy payload
        int payloadLength = packetLength - PacketSize.Header;
        byte[] payloadArray = new byte[payloadLength];
        fixed (byte* payloadPtr = payloadArray)
        {
            Buffer.MemoryCopy(
                source + PacketSize.Header,   // source
                payloadPtr,                   // destination
                payloadLength,                // destinationSizeInBytes  
                payloadLength                 // sourceBytesToCopy
            );
        }

        // Set Payload after copying
        packet = packet with { Payload = new ReadOnlyMemory<byte>(payloadArray) };
    }
}