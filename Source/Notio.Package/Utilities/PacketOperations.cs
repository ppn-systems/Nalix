using System;
using System.Buffers;

namespace Notio.Package.Utilities;

public static class PacketOperations
{
    /// <summary>
    /// Creates an independent copy of a Packet.
    /// </summary>
    /// <param name="packet">The Packet instance to be cloned.</param>
    /// <returns>A new Packet that is a copy of the original.</returns>
    public static Packet Clone(Packet packet)
    {
        // Copy payload with safety check
        byte[] payloadCopy = new byte[packet.Payload.Length];
        packet.Payload.Span.CopyTo(payloadCopy);

        return new Packet(packet.Type, packet.Flags, packet.Priority, packet.Command, payloadCopy);
    }

    /// <summary>
    /// Attempts to clone the packet without throwing an error, returning success or failure.
    /// </summary>
    /// <param name="packet">The Packet to clone.</param>
    /// <param name="clonedPacket">The cloned Packet, if successful.</param>
    /// <returns>True if the packet was cloned successfully, otherwise false.</returns>
    public static bool TryClone(Packet packet, out Packet clonedPacket)
    {
        try
        {
            clonedPacket = Clone(packet);
            return true;
        }
        catch
        {
            clonedPacket = default;
            return false;
        }
    }

    /// <summary>
    /// Creates a clone with performance optimizations.
    /// </summary>
    /// <param name="packet">The Packet to clone.</param>
    /// <returns>A new Packet that is a copy of the original, with performance improvements.</returns>
    public static Packet CloneOptimized(Packet packet)
    {
        // Optimize payload copy for cases where a new array is not needed
        if (packet.Payload.Length <= Packet.MinPacketSize)
            return packet;

        byte[] payloadCopy = ArrayPool<byte>.Shared.Rent(packet.Payload.Length);
        packet.Payload.Span.CopyTo(payloadCopy);
        Packet newPacket = new(packet.Type, packet.Flags, packet.Priority, packet.Command,
            new ReadOnlyMemory<byte>(payloadCopy, 0, packet.Payload.Length));

        return newPacket;
    }
}