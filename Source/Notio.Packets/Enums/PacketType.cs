namespace Notio.Packets.Enums;

// <summary>
/// Represents different types of payloads used in network packets.
/// </summary>
public enum PacketType : byte
{
    None = 0,
    Int = 1,
    String = 2,
    List = 3,
    Long = 4,
    Json = 5,
    Xaml = 6,
    Binary = 7,
    File = 8,
    Image = 9,
    Video = 10,
    Audio = 11
}