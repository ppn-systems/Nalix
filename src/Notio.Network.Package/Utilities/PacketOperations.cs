using Notio.Common.Package;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities;

/// <summary>
/// Provides utility methods for working with packets.
/// </summary>
[SkipLocalsInit]
public static class PacketOperations
{
    /// <summary>
    /// Creates an independent copy of a <see cref="IPacket"/>.
    /// </summary>
    /// <param name="packet">The <see cref="IPacket"/> instance to be cloned.</param>
    /// <returns>A new <see cref="IPacket"/> that is a copy of the original.</returns>
    public static IPacket Clone(IPacket packet)
    {
        // Copy payload with safety check
        byte[] payloadCopy = new byte[packet.Payload.Length];
        packet.Payload.Span.CopyTo(payloadCopy);

        return new Packet(packet.Id, packet.Type,
            packet.Flags, packet.Priority, packet.Command,
            packet.Timestamp, packet.Checksum, payloadCopy);
    }

    /// <summary>
    /// Attempts to clone the <see cref="IPacket"/> without throwing an error, returning success or failure.
    /// </summary>
    /// <param name="packet">The <see cref="IPacket"/> to clone.</param>
    /// <param name="clonedPacket">The cloned <see cref="IPacket"/>, if successful.</param>
    /// <returns>True if the packet was cloned successfully, otherwise false.</returns>
    public static bool TryClone(IPacket packet, out IPacket? clonedPacket)
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
