namespace Nalix.Common.Packets.Enums;

/// <summary>
/// Defines the priority levels of a packet.
/// </summary>
public enum PacketPriority : System.Byte
{
    /// <summary>
    /// Normal priority.
    /// </summary>
    Normal = 0x00,

    /// <summary>
    /// Low priority.
    /// </summary>
    Low = 0x01,

    /// <summary>
    /// Medium priority.
    /// </summary>
    Medium = 0x02,

    /// <summary>
    /// High priority.
    /// </summary>
    High = 0x03,

    /// <summary>
    /// Urgent priority.
    /// </summary>
    Urgent = 0x04
}
