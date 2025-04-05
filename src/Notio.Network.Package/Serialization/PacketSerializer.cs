using Notio.Common.Package.Metadata;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Notio.Network.Package.Serialization;

/// <summary>
/// Provides high-performance methods for serializing and deserializing network packets.
/// </summary>
[SkipLocalsInit]
public static partial class PacketSerializer
{
    // Pre-allocated buffers for stream operations
    private static readonly ThreadLocal<byte[]> _threadLocalHeaderBuffer = new(
        () => new byte[PacketSize.Header], true);

    /// <summary>
    /// Writes the header of a packet into the provided buffer in a structured binary format.
    /// </summary>
    /// <param name="buffer">The target span to write the packet header into. Must be large enough to hold all header fields.</param>
    /// <param name="totalSize">The total size of the packet, including header and payload.</param>
    /// <param name="id">The packet identifier.</param>
    /// <param name="timestamp">The timestamp representing when the packet was created or sent.</param>
    /// <param name="checksum">The CRC32 or other checksum used for data integrity validation.</param>
    /// <param name="packet">The packet object containing meta fields (number, type, flags, priority) to write into the buffer.</param>
    /// <remarks>
    /// This method performs direct memory writes and assumes the buffer has correct alignment and offsets defined by <c>PacketOffset</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EmitHeader(
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

    /// <summary>
    /// Copies a payload slice from a given data span and returns it as a <see cref="Memory{Byte}"/> instance.
    /// </summary>
    /// <param name="data">The source span containing the payload data.</param>
    /// <param name="payloadLength">The number of bytes to copy from the start of the data.</param>
    /// <param name="payload">The resulting <see cref="Memory{Byte}"/> containing the copied payload data.</param>
    /// <remarks>
    /// If the input span is not empty and convertible to an array segment, it will be wrapped without allocation.
    /// Otherwise, a new byte array is allocated and filled manually.
    /// </remarks>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MaterializePayload(ReadOnlySpan<byte> data, int payloadLength, out Memory<byte> payload)
    {
        if (payloadLength > 0)
        {
            if (data is { IsEmpty: false } &&
                MemoryMarshal.TryGetArray(data[..payloadLength].ToArray(), out ArraySegment<byte> segment))
            {
                payload = segment;
            }
            else
            {
                byte[] payloadArray = new byte[payloadLength];
                data[..payloadLength].CopyTo(payloadArray);
                payload = payloadArray;
            }
        }
        else
        {
            payload = Memory<byte>.Empty;
        }
    }
}
