using Notio.Common.Package.Metadata;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Network.Package.Serialization;

public static partial class PacketSerializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitHeader(
        Span<byte> buffer, int totalSize,
        ushort id, ulong timestamp,
        uint checksum, in Packet packet)
    {
        MemoryMarshal.Write(buffer, in totalSize);
        MemoryMarshal.Write(buffer[PacketOffset.Id..], in id);
        MemoryMarshal.Write(buffer[PacketOffset.Timestamp..], in timestamp);
        MemoryMarshal.Write(buffer[PacketOffset.Checksum..], in checksum);

        buffer[PacketOffset.Number] = packet.Number;
        buffer[PacketOffset.Type] = (byte)packet.Type;
        buffer[PacketOffset.Flags] = (byte)packet.Flags;
        buffer[PacketOffset.Priority] = (byte)packet.Priority;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void EmitHeaderUnsafe(
        byte* buffer, int totalSize,
        ushort id, ulong timestamp,
        uint checksum, in Packet packet)
    {
        *(int*)buffer = totalSize;
        *(ushort*)(buffer + PacketOffset.Id) = id;
        *(ulong*)(buffer + PacketOffset.Timestamp) = timestamp;
        *(uint*)(buffer + PacketOffset.Checksum) = checksum;

        buffer[PacketOffset.Number] = packet.Number;
        buffer[PacketOffset.Type] = (byte)packet.Type;
        buffer[PacketOffset.Flags] = (byte)packet.Flags;
        buffer[PacketOffset.Priority] = (byte)packet.Priority;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void EmitHeaderUnsafe(
        Span<byte> buffer, int totalSize,
        ushort id, ulong timestamp,
        uint checksum, in Packet packet)
    {
        fixed (byte* pBuffer = buffer)
        {
            EmitHeaderUnsafe(pBuffer, totalSize, id, timestamp, checksum, packet);
        }
    }
}
