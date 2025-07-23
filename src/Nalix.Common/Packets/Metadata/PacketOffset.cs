namespace Nalix.Common.Packets.Metadata;

/// <summary>
/// Offsets for the components in the packet header.
/// </summary>
public static class PacketOffset
{
    /// <summary>
    /// Offset for the packet length.
    /// </summary>
    public const System.Int32 Length = 0;

    /// <summary>
    /// Offset for the packet command.
    /// </summary>
    public const System.Int32 OpCode = Length + PacketSize.Length;

    /// <summary>
    /// Offset for the packet ProtocolType.
    /// </summary>
    public const System.Int32 Protocol = OpCode + PacketSize.OpCode;

    /// <summary>
    /// Offset for the packet checksum.
    /// </summary>
    public const System.Int32 Checksum = Protocol + PacketSize.Protocol;

    /// <summary>
    /// Offset for the packet timestamp.
    /// </summary>
    public const System.Int32 Timestamp = Checksum + PacketSize.Checksum;

    /// <summary>
    /// Offset for the packet type.
    /// </summary>
    public const System.Int32 Type = Timestamp + PacketSize.Timestamp;

    /// <summary>
    /// Offset for the packet flags.
    /// </summary>
    public const System.Int32 Flags = Type + PacketSize.Type;

    /// <summary>
    /// Offset for the packet priority.
    /// </summary>
    public const System.Int32 Priority = Flags + PacketSize.Flags;

    /// <summary>
    /// Offset for the packet payload.
    /// </summary>
    public const System.Int32 Payload = Priority + PacketSize.Priority;
}