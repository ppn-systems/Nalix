namespace Notio.Network.Package.Utilities;

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

        return new Packet(packet.Id, packet.Type,
            packet.Flags, packet.Priority, packet.Command,
            packet.Timestamp, packet.Checksum, payloadCopy);
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
}