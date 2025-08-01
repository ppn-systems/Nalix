namespace Nalix.Common.Packets;

/// <summary>
/// Represents default values and constants for packet configurations.
/// This class contains minimum and maximum packet sizes, as well as thresholds for optimized memory operations.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// The threshold size (in bytes) for using heap-based memory allocation.
    /// This value represents the maximum size for which memory should be allocated from the heap instead of the stack.
    /// </summary>
    public const System.Int32 HeapAllocLimit = 1024;

    /// <summary>
    /// Maximum stack allocation size (in bytes).
    /// </summary>
    public const System.Int32 StackAllocLimit = 512;

    /// <summary>
    /// The maximum allowed packet size (in bytes), which is 64KB (65535 bytes).
    /// </summary>
    public const System.Int32 PacketSizeLimit = 0xFFFF;
}
