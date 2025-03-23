namespace Notio.Network.Package;

/// <summary>
/// Represents default values and constants for packet configurations.
/// This class contains minimum and maximum packet sizes, as well as thresholds for optimized memory operations.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// Threshold for optimized vectorized memory comparison (32 bytes).
    /// This can be used for faster operations when comparing packet data.
    /// </summary>
    public const int Vector256Size = 0x20;

    /// <summary>
    /// The maximum allowed packet size (in bytes), which is 64KB (65535 bytes).
    /// </summary>
    public const ushort MaxPacketSize = 0xFFFF;
}
