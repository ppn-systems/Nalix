using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Network.Package.Engine.Serialization;

public static partial class PacketSerializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MaterializePayloadFast(ReadOnlySpan<byte> data, int payloadLength, out Memory<byte> payload)
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

    /// <summary>
    /// Efficiently materializes a payload using unsafe code when appropriate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void MaterializePayloadFastUnsafe(ReadOnlySpan<byte> data, int payloadSize, out Memory<byte> payload)
    {
        // For empty payloads, avoid allocation
        if (payloadSize == 0)
        {
            payload = Memory<byte>.Empty;
            return;
        }

        // For small payloads, use a pooled buffer
        if (payloadSize <= 4096)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(payloadSize);

            // Fast copy using unsafe pointer arithmetic for small-to-medium payloads
            fixed (byte* source = data)
            fixed (byte* destination = buffer)
            {
                Buffer.MemoryCopy(source, destination, payloadSize, payloadSize);
            }

            payload = buffer.AsMemory(0, payloadSize);
            // Note: The caller is responsible for returning this buffer to the pool
        }
        else
        {
            // For large payloads, allocate directly
            byte[] buffer = new byte[payloadSize];

            fixed (byte* source = data)
            fixed (byte* destination = buffer)
            {
                Buffer.MemoryCopy(source, destination, payloadSize, payloadSize);
            }

            payload = buffer;
        }
    }
}
