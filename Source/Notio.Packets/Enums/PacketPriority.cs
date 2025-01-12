namespace Notio.Packets.Enums;

/// <summary>
/// Defines the priority levels of a packet.
/// </summary>
public enum PacketPriority : byte
{
    None = 0x00,

    Low = 0x01,
    Medium = 0x02,
    High = 0x03, 
    Urgent = 0x04
}