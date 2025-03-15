using Notio.Common.Package;
using System.Runtime.InteropServices;

namespace Notio.Network.Package.Metadata;

/// <summary>
/// Represents the header structure of a packet, containing metadata such as the packet type, flags, priority, command, timestamp, and checksum.
/// This structure is used to describe the essential details of a packet for efficient transmission.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketHeader"/> struct from the provided <see cref="Packet"/>.
/// </remarks>
/// <param name="packet">The packet from which the header will be extracted.</param>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct PacketHeader(in IPacket packet)
{
    /// <summary>
    /// Gets the total length of the packet header, including length, id, type, flags, priority, command, timestamp, checksum and payload.
    /// </summary>
    public readonly ushort Length = packet.Length;

    /// <summary>
    /// Gets the unique identifier for the packet instance.
    /// </summary>
    public readonly byte Id = packet.Id;

    /// <summary>
    /// Gets the type of the packet, which specifies the kind of packet.
    /// </summary>
    public readonly byte Type = packet.Type;

    /// <summary>
    /// Gets or sets the flags associated with the packet, used for additional control or state information.
    /// </summary>
    public readonly byte Flags = packet.Flags;

    /// <summary>
    /// Gets the priority level of the packet, which can affect how the packet is processed or prioritized.
    /// </summary>
    public readonly byte Priority = packet.Priority;

    /// <summary>
    /// Gets the command associated with the packet, which can specify an operation or request type.
    /// </summary>
    public readonly ushort Command = packet.Command;

    /// <summary>
    /// Gets the timestamp when the packet was created. This is a unique timestamp based on the system's current time.
    /// </summary>
    public readonly ulong Timestamp = packet.Timestamp;

    /// <summary>
    /// Gets the checksum of the packet, computed based on the payload. Used for integrity validation.
    /// </summary>
    public readonly uint Checksum = packet.Checksum;
}
