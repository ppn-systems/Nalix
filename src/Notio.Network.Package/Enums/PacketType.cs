namespace Notio.Network.Package.Enums;

/// <summary>
/// Represents different types of payloads used in network packets.
/// </summary>
public enum PacketType : byte
{
    /// <summary>
    /// General type: None.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Primitive type: Integer.
    /// </summary>
    Int = 0x01,

    /// <summary>
    /// Primitive type: Long.
    /// </summary>
    Long = 0x02,

    /// <summary>
    /// Primitive type: Float.
    /// </summary>
    Float = 0x03,

    /// <summary>
    /// Primitive type: Double.
    /// </summary>
    Double = 0x04,

    /// <summary>
    /// Primitive type: Boolean.
    /// </summary>
    Bool = 0x05,

    /// <summary>
    /// Primitive type: String.
    /// </summary>
    String = 0x06,

    /// <summary>
    /// Primitive type: List.
    /// </summary>
    List = 0x07,

    /// <summary>
    /// Structured data: JSON.
    /// </summary>
    Json = 0x0A,

    /// <summary>
    /// Structured data: XAML.
    /// </summary>
    Xaml = 0x0B,

    /// <summary>
    /// Structured data: XML.
    /// </summary>
    Xml = 0x0C,

    /// <summary>
    /// Structured data: CSV.
    /// </summary>
    Csv = 0x0D,

    /// <summary>
    /// Structured data: YAML.
    /// </summary>
    Yaml = 0x0E,

    /// <summary>
    /// Structured data: HTML.
    /// </summary>
    Html = 0x0F,

    /// <summary>
    /// Structured data: Protocol Buffers.
    /// </summary>
    Protobuf = 0x10,

    /// <summary>
    /// Media type: Binary.
    /// </summary>
    Binary = 0x14,

    /// <summary>
    /// Media type: File.
    /// </summary>
    File = 0x15,

    /// <summary>
    /// Media type: Image.
    /// </summary>
    Image = 0x16,

    /// <summary>
    /// Media type: Video.
    /// </summary>
    Video = 0x17,

    /// <summary>
    /// Media type: Audio.
    /// </summary>
    Audio = 0x18,

    /// <summary>
    /// Media type: SVG.
    /// </summary>
    Svg = 0x19,

    /// <summary>
    /// Media type: GIF.
    /// </summary>
    Gif = 0x1A,

    /// <summary>
    /// Media type: 3D Model.
    /// </summary>
    Model3D = 0x1B,

    /// <summary>
    /// Encoded or compressed data: Base64.
    /// </summary>
    Base64 = 0x1E,

    /// <summary>
    /// Encoded or compressed data: Compressed.
    /// </summary>
    Compressed = 0x1F,

    /// <summary>
    /// Miscellaneous: Timestamp.
    /// </summary>
    Timestamp = 0x28,

    /// <summary>
    /// Miscellaneous: UUID.
    /// </summary>
    Uuid = 0x29,

    /// <summary>
    /// Miscellaneous: Dictionary.
    /// </summary>
    Dictionary = 0x2A,

    /// <summary>
    /// Custom or undefined type.
    /// </summary>
    Custom = 0xFF
}