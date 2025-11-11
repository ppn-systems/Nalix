// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Configuration.Binding;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Configuration for buffer settings with validation and performance/security options.
/// </summary>
public sealed class BufferConfig : ConfigurationLoader
{
    #region Backing Fields

    private System.Int32 _totalBuffers = 100;
    private System.Int32 _trimIntervalMinutes = 5;
    private System.Int32 _deepTrimIntervalMinutes = 30;
    private System.Double _adaptiveGrowthFactor = 2.0;
    private System.Double _maxMemoryPercentage = 0.25;
    private System.Int32 _autoTuneOperationThreshold = 10_000;
    private System.Double _expandThresholdPercent = 0.25;
    private System.Double _shrinkThresholdPercent = 0.50;
    private System.Int32 _minimumIncrease = 4;
    private System.Int32 _maxBufferIncreaseLimit = 1024;
    private System.String _bufferAllocations = "256,0.05; 512,0.10; 1024,0.25; 2048,0.20; 4096,0.15; 8192,0.10; 16384,0.10; 32768,0.03; 65536,0.02";

    #endregion Backing Fields

    #region Properties

    /// <summary>
    /// The total number of buffers to create across all pools.
    /// </summary>
    public System.Int32 TotalBuffers
    {
        get => _totalBuffers;
        set => _totalBuffers = value > 0
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(TotalBuffers), "Total buffers must be greater than zero.");
    }

    /// <summary>
    /// Enables memory trimming to periodically recover unused buffers.
    /// </summary>
    public System.Boolean EnableMemoryTrimming { get; set; } = true;

    /// <summary>
    /// Time interval in minutes between memory trimming operations.
    /// </summary>
    public System.Int32 TrimIntervalMinutes
    {
        get => _trimIntervalMinutes;
        set => _trimIntervalMinutes = value is > 0 and <= 60
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(TrimIntervalMinutes), "Trim interval must be between 1 and 60 minutes.");
    }

    /// <summary>
    /// Time interval in minutes for deep trimming operations.
    /// </summary>
    public System.Int32 DeepTrimIntervalMinutes
    {
        get => _deepTrimIntervalMinutes;
        set => _deepTrimIntervalMinutes = value is > 0 and <= 24 * 60
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(DeepTrimIntervalMinutes), "Deep trim interval must be between 1 and 1440 minutes.");
    }

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
    /// Recommended range: [1.25, 4.0].
    /// </summary>
    public System.Double AdaptiveGrowthFactor
    {
        get => _adaptiveGrowthFactor;
        set => _adaptiveGrowthFactor = (value is >= 1.25 and <= 4.0)
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(AdaptiveGrowthFactor), "Adaptive growth factor must be in range [1.25, 4.0].");
    }

    /// <summary>
    /// Maximum percentage of system memory to use for buffer pools.
    /// </summary>
    public System.Double MaxMemoryPercentage
    {
        get => _maxMemoryPercentage;
        set => _maxMemoryPercentage = (value is > 0.0 and <= 0.90)
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(MaxMemoryPercentage), "Max percentage must be in range (0, 0.90].");
    }

    /// <summary>
    /// Enable zero-memory clear on buffer return for security-sensitive applications.
    /// This may reduce performance but increases security.
    /// </summary>
    public System.Boolean SecureClear { get; set; } = false;

    /// <summary>
    /// Enable queue compaction to reduce memory fragmentation.
    /// </summary>
    public System.Boolean EnableQueueCompaction { get; set; } = false;

    /// <summary>
    /// The number of buffer rent/return operations between auto-tuning cycles.
    /// </summary>
    public System.Int32 AutoTuneOperationThreshold
    {
        get => _autoTuneOperationThreshold;
        set => _autoTuneOperationThreshold = value >= 10
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(AutoTuneOperationThreshold), "Threshold must be >= 10.");
    }

    /// <summary>
    /// Whether to fall back to <see cref="System.Buffers.ArrayPool{T}.Shared"/> when a requested size has no suitable pool.
    /// </summary>
    public System.Boolean FallbackToArrayPool { get; set; } = true;

    /// <summary>
    /// Free/Total ratio threshold to trigger expansion (e.g., 0.25 means expand when free &lt;= 25% of total).
    /// </summary>
    public System.Double ExpandThresholdPercent
    {
        get => _expandThresholdPercent;
        set => _expandThresholdPercent = (value is > 0.0 and < 1.0)
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(ExpandThresholdPercent), "Value must be in range (0,1).");
    }

    /// <summary>
    /// Free/Total ratio threshold to allow shrink (e.g., 0.50 means shrink when free &gt;= 50% of total).
    /// </summary>
    public System.Double ShrinkThresholdPercent
    {
        get => _shrinkThresholdPercent;
        set => _shrinkThresholdPercent = (value is > 0.0 and < 1.0)
            ? value
            : throw new System.ArgumentOutOfRangeException(nameof(ShrinkThresholdPercent), "Value must be in range (0,1).");
    }

    /// <summary>
    /// Minimum increase step when growing a pool.
    /// </summary>
    public System.Int32 MinimumIncrease
    {
        get => _minimumIncrease;
        set => _minimumIncrease = value >= 1 ? value : throw new System.ArgumentOutOfRangeException(nameof(MinimumIncrease));
    }

    /// <summary>
    /// Maximum one-shot buffer increase to cap memory spikes.
    /// </summary>
    public System.Int32 MaxBufferIncreaseLimit
    {
        get => _maxBufferIncreaseLimit;
        set => _maxBufferIncreaseLimit = value >= 1 ? value : throw new System.ArgumentOutOfRangeException(nameof(MaxBufferIncreaseLimit));
    }

    /// <summary>
    /// A string representing buffer allocations. Example: "1024,0.40; 2048,0.25; 4096,0.20".
    /// </summary>
    public System.String BufferAllocations
    {
        get => _bufferAllocations;
        set => _bufferAllocations = System.String.IsNullOrWhiteSpace(value)
            ? throw new System.ArgumentException("Buffer allocation string cannot be null/empty.", nameof(BufferAllocations))
            : value;
    }


    #endregion Properties
}