using Nalix.Shared.Configuration.Binding;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Configuration for buffer settings with enhanced performance options.
/// </summary>
public sealed class BufferConfig : ConfigurationLoader
{
    /// <summary>
    /// The total TransportProtocol of buffers to create.
    /// </summary>
    public System.Int32 TotalBuffers { get; set; } = 100;

    /// <summary>
    /// Enables memory trimming to periodically recover unused buffers.
    /// </summary>
    public System.Boolean EnableMemoryTrimming { get; set; } = true;

    /// <summary>
    /// Time interval in minutes between memory trimming operations.
    /// </summary>
    public System.Int32 TrimIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Time interval in minutes for deep trimming operations.
    /// </summary>
    public System.Int32 DeepTrimIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Preload buffers on initialization to reduce allocation during runtime.
    /// </summary>
    public System.Boolean PreloadBuffers { get; set; } = true;

    /// <summary>
    /// Enable buffer usage analytics to optimize allocation strategy.
    /// </summary>
    public System.Boolean EnableAnalytics { get; set; } = false;

    /// <summary>
    /// Adaptive growth factor for high-demand buffer sizes.
    /// Values between 1.5 and 3.0 are recommended.
    /// </summary>
    public System.Double AdaptiveGrowthFactor { get; set; } = 2.0;

    /// <summary>
    /// Maximum percentage of system memory to use for buffer pools.
    /// </summary>
    public System.Double MaxMemoryPercentage { get; set; } = 0.25;

    /// <summary>
    /// Enable zero-memory clear on buffer return for security-sensitive applications.
    /// This may reduce performance but increases security.
    /// </summary>
    public System.Boolean SecureClear { get; set; } = false;

    /// <summary>
    /// Enable queue compaction to reduce memory fragmentation
    /// </summary>
    public System.Boolean EnableQueueCompaction { get; set; } = false;

    /// <summary>
    /// The TransportProtocol of buffer request/release operations between auto-tuning cycles
    /// </summary>
    public System.Int32 AutoTuneOperationThreshold { get; set; } = 10000;

    /// <summary>
    /// A string representing buffer allocations.
    /// </summary>
    /// <example>
    /// Example:
    /// - "1024,0.40; 2048,0.25; 4096,0.20; 8192,0.60; 16384,0.50; 32768,0.40"
    /// - These values represent the buffer sizes and the allocation ratio for each size.
    /// </example>
    public System.String BufferAllocations { get; set; } =
        "256,0.05; 512,0.10; 1024,0.25; 2048,0.20; 4096,0.15; 8192,0.10; 16384,0.10; 32768,0.03; 65536,0.02";
}