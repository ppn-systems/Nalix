using System.Buffers;

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
    public const int SimdThreshold = 0x20;

    /// <summary>
    /// The maximum allowed packet size (in bytes), which is 64KB (65535 bytes).
    /// </summary>
    public const ushort PacketSizeLimit = 0xFFFF;

    /// <summary>
    /// Maximum stack allocation size (in bytes).
    /// </summary>
    public const int StackAllocLimit = 512;

    /// <summary>
    /// Shared byte array pool for efficient memory usage.
    /// </summary>
    public static readonly ArrayPool<byte> SharedBytePool = ArrayPool<byte>.Shared;
}
