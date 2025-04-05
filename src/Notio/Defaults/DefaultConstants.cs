using System.Text;

namespace Notio.Defaults;

/// <summary>
/// Represents default values and constants for configurations.
/// </summary>
public static class DefaultConstants
{
    /// <summary>
    /// The amount of memory to use in kibibytes (KiB).
    /// </summary>
    public const uint MemoryCostKiB = 4096;

    /// <summary>
    /// The Number of threads and compute lanes to use.
    /// </summary>
    public const uint ParallelismDegree = 1;

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
    /// The Number of microseconds in one second (1,000,000).
    /// This value is used for time conversions and time-based calculations.
    /// </summary>
    public const long MicrosecondsInSecond = 1_000_000L;

    /// <summary>
    /// The default encoding used for JSON serialization and deserialization.
    /// </summary>
    public static Encoding DefaultEncoding { get; set; } = Encoding.UTF8;
}
