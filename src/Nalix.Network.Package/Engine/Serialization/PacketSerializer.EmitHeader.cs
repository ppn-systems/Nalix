using Nalix.Common.Package.Metadata;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Network.Package.Engine.Serialization;

public static partial class PacketSerializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitHeader(
        Span<byte> buffer, int totalSize,
        ushort id, ulong timestamp,
        uint checksum, ushort code, in Packet packet)
    {
        // Writing the first part of the header in one block (could optimize by grouping)
        MemoryMarshal.Write(buffer, in totalSize);
        MemoryMarshal.Write(buffer[PacketOffset.Id..], in id);
        MemoryMarshal.Write(buffer[PacketOffset.Timestamp..], in timestamp);
        MemoryMarshal.Write(buffer[PacketOffset.Checksum..], in checksum);
        MemoryMarshal.Write(buffer[PacketOffset.Code..], in code);

        // Writing the packet-specific fields
        buffer[PacketOffset.Number] = packet.Number;
        buffer[PacketOffset.Type] = (byte)packet.Type;
        buffer[PacketOffset.Flags] = (byte)packet.Flags;
        buffer[PacketOffset.Priority] = (byte)packet.Priority;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void EmitHeaderUnsafe(
        byte* buffer, int totalSize,
        ushort id, ulong timestamp,
        uint checksum, ushort code, in Packet packet)
    {
        *(int*)buffer = totalSize;
        *(ushort*)(buffer + PacketOffset.Id) = id;
        *(ulong*)(buffer + PacketOffset.Timestamp) = timestamp;
        *(uint*)(buffer + PacketOffset.Checksum) = checksum;
        *(ushort*)(buffer + PacketOffset.Code) = code;

        buffer[PacketOffset.Number] = packet.Number;
        buffer[PacketOffset.Type] = (byte)packet.Type;
        buffer[PacketOffset.Flags] = (byte)packet.Flags;
        buffer[PacketOffset.Priority] = (byte)packet.Priority;
    }
}
