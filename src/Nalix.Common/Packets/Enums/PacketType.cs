namespace Nalix.Common.Packets.Enums;

/// <summary>
/// Represents different types of payloads used in network packets.
/// </summary>
public enum PacketType : System.Byte
{
    #region General Types

    /// <summary>
    /// General type: None.
    /// </summary>
    None = 0x00,

    #endregion General Types

    #region Primitive Types

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

    #endregion Primitive Types

    #region Structured Data Types

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
    /// Structured data: ProtocolType Buffers.
    /// </summary>
    Protobuf = 0x10,

    #endregion Structured Data Types

    #region Media Types

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

    #endregion Media Types

    #region Encoded or Compressed Data Types

    /// <summary>
    /// Encoded or compressed data: Base64.
    /// </summary>
    Base64 = 0x1E,

    /// <summary>
    /// Encoded or compressed data: Compressed.
    /// </summary>
    Compressed = 0x1F,

    #endregion Encoded or Compressed Data Types

    #region Miscellaneous Types

    /// <summary>
    /// Miscellaneous type: Timestamp.
    /// </summary>
    Timestamp = 0x28,

    /// <summary>
    /// Miscellaneous type: UUID.
    /// </summary>
    Uuid = 0x29,

    /// <summary>
    /// Miscellaneous type: Dictionary.
    /// </summary>
    Dictionary = 0x2A,

    /// <summary>
    /// Miscellaneous type: Acknowledgment.
    /// </summary>
    Acknowledgment = 0x2B,

    /// <summary>
    /// Miscellaneous type: Object.
    /// </summary>
    Object = 0x2C,

    #endregion Miscellaneous Types
}
