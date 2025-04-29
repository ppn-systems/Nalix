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
    /// The threshold size (in bytes) for using stack-based memory allocation.
    /// This value represents the maximum size for which memory can be allocated on the stack.
    /// </summary>
    public const int StackAllocThreshold = 256;

    /// <summary>
    /// The threshold size (in bytes) for using heap-based memory allocation.
    /// This value represents the maximum size for which memory should be allocated from the heap instead of the stack.
    /// </summary>
    public const int HeapAllocThreshold = 1024;

    /// <summary>
    /// Shared byte array pool for efficient memory usage.
    /// </summary>
    public static readonly System.Buffers.ArrayPool<byte> SharedBytePool = System.Buffers.ArrayPool<byte>.Shared;
}
