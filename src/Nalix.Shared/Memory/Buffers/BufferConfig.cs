// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.Injection;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Configuration for buffer settings with validation and performance/security options.
/// </summary>
public sealed class BufferConfig : ConfigurationLoader
{
    #region Properties

    /// <summary>
    /// The total number of buffers to create across all pools.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "TotalBuffers must be greater than 0.")]
    public System.Int32 TotalBuffers { get; set; } = 100;

    /// <summary>
    /// Enables memory trimming to periodically recover unused buffers.
    /// </summary>
    public System.Boolean EnableMemoryTrimming { get; set; } = true;

    /// <summary>
    /// Time interval in minutes between memory trimming operations.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 60, ErrorMessage = "TrimIntervalMinutes must be between 1 and 60.")]
    public System.Int32 TrimIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Time interval in minutes for deep trimming operations.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 1440, ErrorMessage = "DeepTrimIntervalMinutes must be between 1 and 1440.")]
    public System.Int32 DeepTrimIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Enable buffer usage analytics to optimize allocation strategy.
    /// </summary>
    public System.Boolean EnableAnalytics { get; set; } = false;

    /// <summary>
    /// Adaptive growth factor for high-demand buffer sizes.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1.25, 4.0, ErrorMessage = "AdaptiveGrowthFactor must be in range [1.25, 4.0].")]
    public System.Double AdaptiveGrowthFactor { get; set; } = 2.0;

    /// <summary>
    /// Maximum percentage of system memory to use for buffer pools.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.Double), "0.000001", "0.90", ErrorMessage = "MaxMemoryPercentage must be in (0, 0.90].")]
    public System.Double MaxMemoryPercentage { get; set; } = 0.25;

    /// <summary>
    /// Enable zero-memory clear on buffer return for security-sensitive applications.
    /// </summary>
    public System.Boolean SecureClear { get; set; } = false;

    /// <summary>
    /// Enable queue compaction to reduce memory fragmentation.
    /// </summary>
    public System.Boolean EnableQueueCompaction { get; set; } = false;

    /// <summary>
    /// The number of buffer rent/return operations between auto-tuning cycles.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(10, System.Int32.MaxValue, ErrorMessage = "AutoTuneOperationThreshold must be >= 10.")]
    public System.Int32 AutoTuneOperationThreshold { get; set; } = 10_000;

    /// <summary>
    /// Whether to fall back to <see cref="System.Buffers.ArrayPool{T}.Shared"/> when a requested size has no suitable pool.
    /// </summary>
    public System.Boolean FallbackToArrayPool { get; set; } = true;

    /// <summary>
    /// Free/Total ratio threshold to trigger expansion.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.Double), "0.000001", "0.999999", ErrorMessage = "ExpandThresholdPercent must be in (0,1).")]
    public System.Double ExpandThresholdPercent { get; set; } = 0.25;

    /// <summary>
    /// Free/Total ratio threshold to allow shrink.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.Double), "0.000001", "0.999999", ErrorMessage = "ShrinkThresholdPercent must be in (0,1).")]
    public System.Double ShrinkThresholdPercent { get; set; } = 0.50;

    /// <summary>
    /// Minimum increase step when growing a pool.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "MinimumIncrease must be at least 1.")]
    public System.Int32 MinimumIncrease { get; set; } = 4;

    /// <summary>
    /// Maximum one-shot buffer increase to cap memory spikes.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "MaxBufferIncreaseLimit must be at least 1.")]
    public System.Int32 MaxBufferIncreaseLimit { get; set; } = 1024;

    /// <summary>
    /// A string representing buffer allocations. Example: "1024,0.40; 2048,0.25; 4096,0.20".
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "BufferAllocations is required.")]
    [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "BufferAllocations cannot be empty.")]
    public System.String BufferAllocations { get; set; } =
        "256,0.05; 512,0.10; 1024,0.25; 2048,0.20; 4096,0.15; 8192,0.10; 16384,0.10; 32768,0.03; 65536,0.02";

    /// <summary>
    /// Maximum memory in bytes that buffer pools can use. 0 means no limit.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, System.Int64.MaxValue, ErrorMessage = "MaxMemoryBytes cannot be negative.")]
    public System.Int64 MaxMemoryBytes { get; set; } = 0;

    #endregion Properties

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);

        if (ExpandThresholdPercent >= ShrinkThresholdPercent)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("ExpandThresholdPercent must be less than ShrinkThresholdPercent.");
        }

        // 2. Kiểm tra định dạng BufferAllocations
        try
        {
            var allocations = ParseBufferAllocations(BufferAllocations);

            System.Double totalRatio = 0;
            System.Int32 lastSize = 0;

            foreach (var (size, ratio) in allocations)
            {
                if (size > lastSize)
                {
                    totalRatio += ratio;
                    lastSize = size;
                    continue;
                }

                throw new System.ComponentModel.DataAnnotations.ValidationException($"BufferAllocations sizes must be strictly increasing (got {lastSize} then {size}).");
            }

            if (totalRatio > 1.01)
            {
                throw new System.ComponentModel.DataAnnotations.ValidationException($"Sum of buffer allocation ratios exceeds 1.0 ({totalRatio}).");
            }
        }
        catch (System.Exception ex)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException($"Invalid BufferAllocations: {ex.Message}");
        }

        // 3. MaxMemoryBytes & MaxMemoryPercentage sanity check
        if (MaxMemoryBytes > 0 && MaxMemoryPercentage > 0.90)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("Cannot specify both MaxMemoryBytes and MaxMemoryPercentage > 0.90.");
        }
        // 4. AdaptiveGrowthFactor logic
        if (AdaptiveGrowthFactor * MinimumIncrease > MaxBufferIncreaseLimit)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("AdaptiveGrowthFactor * MinimumIncrease must be <= MaxBufferIncreaseLimit.");
        }
        // 5. AutoTuneOperationThreshold logic
        if (AutoTuneOperationThreshold < TotalBuffers)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("AutoTuneOperationThreshold should normally be >= TotalBuffers.");
        }
    }

    #region Parsing

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.String, (System.Int32, System.Double)[]> _allocationPatternCache = new();

    /// <summary>
    /// Parses the buffer allocation settings with caching for repeated configurations.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static (System.Int32, System.Double)[] ParseBufferAllocations(System.String bufferAllocationsString)
    {
        return System.String.IsNullOrWhiteSpace(bufferAllocationsString)
            ? throw new System.ArgumentException($"[{nameof(BufferConfig)}] The input string must not be blank.", nameof(bufferAllocationsString))
            : _allocationPatternCache.GetOrAdd(bufferAllocationsString, key =>
            {
                try
                {
                    var allocations = PARSE_ALLOCATIONS(key, bufferAllocationsString);

                    System.Double totalAllocation = System.Linq.Enumerable.Sum(allocations, a => a.ratio);
                    return totalAllocation > 1.1
                        ? throw new System.ArgumentException($"[{nameof(BufferConfig)}] Total allocation ratio ({totalAllocation:F2}) exceeds 1.0.")
                        : ((System.Int32, System.Double)[])allocations;
                }
                catch (System.Exception ex) when (ex is System.FormatException or System.ArgumentException or System.OverflowException or System.ArgumentOutOfRangeException)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[SH.{nameof(BufferConfig)}:Internal] " +
                                                   $"alloc-parse-fail str='{bufferAllocationsString}' msg={ex.Message}");

                    throw new System.ArgumentException(
                        $"[{nameof(BufferConfig)}] Malformed allocation string. Expected '<size>,<ratio>;...'. ERROR: {ex.Message}");
                }
            });
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (System.Int32 allocationSize, System.Double ratio)[] PARSE_ALLOCATIONS(System.String key, System.String bufferAllocationsString)
    {
        // Split by ';'
        System.String[] pairs = key.Split(';', System.StringSplitOptions.RemoveEmptyEntries);

        var list = new System.Collections.Generic.List<(System.Int32, System.Double)>();

        foreach (System.String pair in pairs)
        {
            System.String[] parts = pair.Split(',', System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new System.FormatException(
                    $"[{nameof(BufferConfig)}] Incorrectly formatted pair: '{pair}'.");
            }

            // Parse size
            if (!System.Int32.TryParse(parts[0].Trim(), out System.Int32 allocationSize) || allocationSize <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(bufferAllocationsString),
                    $"[{nameof(BufferConfig)}] SIZE must be > 0.");
            }

            // Parse ratio
            if (!System.Double.TryParse(parts[1].Trim(), out System.Double ratio) || ratio <= 0 || ratio > 1)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(bufferAllocationsString),
                    $"[{nameof(BufferConfig)}] Ratio must be (0,1].");
            }

            list.Add((allocationSize, ratio));
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));

        return [.. list];
    }

    #endregion Parsing
}