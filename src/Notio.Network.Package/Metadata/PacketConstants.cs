namespace Notio.Network.Package.Metadata;

/// <summary>
/// Represents default values and constants for packet configurations.
/// This class contains minimum and maximum packet sizes, as well as thresholds for optimized memory operations.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// The number of microseconds in one second (1,000,000).
    /// This value is used for time conversions and time-based calculations.
    /// </summary>
    public const long MicrosecondsPerSecond = 1_000_000L;

    /// <summary>
    /// Threshold for optimized vectorized memory comparison (32 bytes).
    /// This can be used for faster operations when comparing packet data.
    /// </summary>
    public const int Vector256Size = 0x20;

    /// <summary>
    /// The maximum allowed packet size (in bytes), which is 64KB (65535 bytes).
    /// </summary>
    public const ushort MaxPacketSize = 0xFFFF;

    /// <summary>
    /// The threshold size (in bytes) for using stack-based memory allocation.
    /// This value represents the maximum size for which memory can be allocated on the stack.
    /// </summary>
    public const int MaxStackAllocSize = 0x100;

    /// <summary>
    /// The threshold size (in bytes) for using heap-based memory allocation.
    /// This value represents the maximum size for which memory should be allocated from the heap instead of the stack.
    /// </summary>
    public const int MaxHeapAllocSize = 0x400;
}
