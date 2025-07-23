namespace Nalix.Common.Packets.Metadata;

/// <summary>
/// Defines the sizes of the components in the packet header.
/// </summary>
public static class PacketSize
{
    /// <summary>
    /// The size of the Length field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 Length = sizeof(System.UInt16);

    /// <summary>
    /// The size of the ProtocolType field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 OpCode = sizeof(System.UInt16);

    /// <summary>
    /// The size of the ProtocolType field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 Protocol = sizeof(System.Byte);

    /// <summary>
    /// The size of the Checksum field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 Checksum = sizeof(System.UInt32);

    /// <summary>
    /// The size of the Timestamp field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 Timestamp = sizeof(System.Int64);

    /// <summary>
    /// The size of the Type field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 Type = sizeof(System.Byte);

    /// <summary>
    /// The size of the Flags field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 Flags = sizeof(System.Byte);

    /// <summary>
    /// The size of the Priority field in the packet header, in bytes.
    /// </summary>
    public const System.Int32 Priority = sizeof(System.Byte);

    /// <summary>
    /// The total size of the packet header, which is the sum of the sizes of all header fields.
    /// </summary>
    public const System.Int32 Header = OpCode + Length + Protocol + Type + Flags + Priority + Timestamp + Checksum;
}
