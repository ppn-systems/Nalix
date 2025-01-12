namespace Notio.Packets.Enums;

/// <summary>
/// Represents different types of payloads used in network packets.
/// </summary>
public enum PacketType : byte
{
    // General types
    None = 0x00,

    // Primitive types
    Int = 0x01,
    Long = 0x02,
    Float = 0x03,
    Double = 0x04,
    Bool = 0x05,
    String = 0x06,
    List = 0x07,

    // Structured data
    Json = 0x0A,
    Xaml = 0x0B,
    Xml = 0x0C,
    Csv = 0x0D,
    Yaml = 0x0E,
    Html = 0x0F,
    Protobuf = 0x10,

    // Media types
    Binary = 0x14,
    File = 0x15,
    Image = 0x16,
    Video = 0x17,
    Audio = 0x18,
    Svg = 0x19,
    Gif = 0x1A,
    Model3D = 0x1B,

    // Encoded or compressed data
    Base64 = 0x1E,
    Compressed = 0x1F,

    // Miscellaneous
    Timestamp = 0x28,
    Uuid = 0x29,
    Dictionary = 0x2A,

    // Custom or undefined type
    Custom = 0xFF
}