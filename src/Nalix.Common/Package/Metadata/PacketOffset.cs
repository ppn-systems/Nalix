namespace Nalix.Common.Package.Metadata;

/// <summary>
/// Offsets for the components in the packet header.
/// </summary>
public static class PacketOffset
{
    /// <summary>
    /// Offset for the packet length.
    /// </summary>
    public const int Length = 0;

    /// <summary>
    /// Offset for the packet command.
    /// </summary>
    public const int Id = Length + PacketSize.Length;

    /// <summary>
    /// Offset for the packet checksum.
    /// </summary>
    public const int Checksum = Id + PacketSize.Id;

    /// <summary>
    /// Offset for the packet timestamp.
    /// </summary>
    public const int Timestamp = Checksum + PacketSize.Checksum;

    /// <summary>
    /// Offset for the packet Number.
    /// </summary>
    public const int Number = Timestamp + PacketSize.Timestamp;

    /// <summary>
    /// Offset for the packet type.
    /// </summary>
    public const int Type = Number + PacketSize.Number;

    /// <summary>
    /// Offset for the packet flags.
    /// </summary>
    public const int Flags = Type + PacketSize.Type;

    /// <summary>
    /// Offset for the packet priority.
    /// </summary>
    public const int Priority = Flags + PacketSize.Flags;

    /// <summary>
    /// Offset for the packet payload.
    /// </summary>
    public const int Payload = Priority + PacketSize.Priority;
}
