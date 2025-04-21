using System.Buffers;

namespace Nalix.Common.Constants;

/// <summary>
/// Represents default values and constants for packet configurations.
/// This class contains minimum and maximum packet sizes, as well as thresholds for optimized memory operations.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// Maximum stack allocation size (in bytes).
    /// </summary>
    public const ushort StackAllocLimit = 512;

    /// <summary>
    /// The maximum allowed packet size (in bytes), which is 64KB (65535 bytes).
    /// </summary>
    public const ushort PacketSizeLimit = 0xFFFF;

    /// <summary>
    /// Shared byte array pool for efficient memory usage.
    /// </summary>
    public static readonly ArrayPool<byte> SharedBytePool = ArrayPool<byte>.Shared;
}
