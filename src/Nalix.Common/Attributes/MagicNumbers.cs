namespace Nalix.Common.Attributes;

/// <summary>
/// Enum representing the magic numbers used to identify different packet types.
/// </summary>
public enum MagicNumbers : System.UInt32
{
    /// <summary>
    /// Magic number for BinaryPacket type.
    /// This number is used to identify packets of type BinaryPacket.
    /// </summary>
    BinaryPacket = 0x12345678,

    /// <summary>
    /// Magic number for LiteralPacket type.
    /// This number is used to identify packets of type LiteralPacket.
    /// </summary>
    LiteralPacket = 0x87654321
}
