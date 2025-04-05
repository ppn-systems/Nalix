using Notio.Common.Exceptions;
using Notio.Common.Package.Metadata;
using System;

namespace Notio.Network.Package.Serialization;

public static partial class PacketSerializer
{
    /// <summary>
    /// Serializes the specified packet to a byte array.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <returns>The serialized byte array representing the packet.</returns>
    public static byte[] Serialize(in Packet packet)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (packet.Payload.Length > ushort.MaxValue)
            throw new PackageException("Payload is too large.");

        if (totalSize <= PacketConstants.StackAllocLimit)
        {
            Span<byte> stackBuffer = stackalloc byte[totalSize];
            WritePacketFast(stackBuffer, in packet);
            return stackBuffer.ToArray();
        }
        else
        {
            byte[] rentedArray = PacketConstants.SharedBytePool.Rent(totalSize);
            try
            {
                WritePacketFast(rentedArray.AsSpan(0, totalSize), in packet);
                return rentedArray.AsSpan(0, totalSize).ToArray();
            }
            finally
            {
                PacketConstants.SharedBytePool.Return(rentedArray, clearArray: true);
            }
        }
    }

    /// <summary>
    /// Attempts to serialize the specified packet to the destination span.
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <param name="destination">The destination span to hold the serialized packet.</param>
    /// <param name="bytesWritten">The Number of bytes written to the destination span.</param>
    /// <returns>Returns true if serialization was successful; otherwise, false.</returns>
    public static bool TrySerialize(in Packet packet, Span<byte> destination, out int bytesWritten)
    {
        int totalSize = PacketSize.Header + packet.Payload.Length;

        if (packet.Payload.Length > ushort.MaxValue || destination.Length < totalSize)
        {
            bytesWritten = 0;
            return false;
        }

        try
        {
            WritePacketFast(destination[..totalSize], in packet);
            bytesWritten = totalSize;
            return true;
        }
        catch
        {
            bytesWritten = 0;
            return false;
        }
    }
}
