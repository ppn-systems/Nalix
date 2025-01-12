using Notio.Packets.Exceptions;
using Notio.Packets.Metadata;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Packets.Serialization;

[SkipLocalsInit]
internal static class PacketSerializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WritePacketFast(Span<byte> buffer, in Packet packet)
    {
        if (buffer.Length < PacketSize.Header + packet.Payload.Length)
            throw new PacketException("Buffer size is too small to write the packet.");

        ref byte bufferStart = ref MemoryMarshal.GetReference(buffer);
        int totalSize = buffer.Length;

        // Write all header fields in one go using pointer arithmetic
        Unsafe.WriteUnaligned(ref bufferStart, (short)totalSize);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Type), packet.Type);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Flags), packet.Flags);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref bufferStart, PacketOffset.Command), packet.Command);

        // Copy payload efficiently
        packet.Payload.Span.CopyTo(buffer[PacketSize.Header..]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Packet ReadPacketFast(ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PacketException("Data size is smaller than the minimum header size.");      

        ref byte dataRef = ref MemoryMarshal.GetReference(data);

        short length = Unsafe.ReadUnaligned<short>(ref dataRef);
        if ((uint)length > data.Length)
            throw new 
                PacketException($"Invalid packet length: {length}. Must be between {PacketSize.Header} and {data.Length}.");

        // Read all fields at once using pointer arithmetic
        return new Packet
        (
            type: Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref dataRef, PacketOffset.Type)),
            flags: Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref dataRef, PacketOffset.Flags)),
            command: Unsafe.ReadUnaligned<short>(ref Unsafe.Add(ref dataRef, PacketOffset.Command)),
            payload: data[PacketSize.Header..length].ToArray()
        );
    }
}