// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Nalix.Common.Diagnostics;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.Injection;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Configuration for buffer settings with validation and performance/security options.
/// </summary>
[IniComment("Buffer pool configuration — controls pool sizing, trimming, adaptive growth, and memory limits")]
public sealed class BufferConfig : ConfigurationLoader
{
    #region Properties

    /// <summary>
    /// The total number of buffers to create across all pools.
    /// </summary>
    [IniComment("Total buffers to create across all pools (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "TotalBuffers must be greater than 0.")]
    public System.Int32 TotalBuffers { get; set; } = 1024;

    /// <summary>
    /// Enables memory trimming to periodically recover unused buffers.
    /// </summary>
    [IniComment("Periodically return unused buffers to reclaim memory")]
    public System.Boolean EnableMemoryTrimming { get; set; } = true;

    /// <summary>
    /// Time interval in minutes between memory trimming operations.
    /// </summary>
    [IniComment("Interval in minutes between light trim cycles (1–60)")]
    [System.ComponentModel.DataAnnotations.Range(1, 60, ErrorMessage = "TrimIntervalMinutes must be between 1 and 60.")]
    public System.Int32 TrimIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Time interval in minutes for deep trimming operations.
    /// </summary>
    [IniComment("Interval in minutes between deep trim cycles (1–1440)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1440, ErrorMessage = "DeepTrimIntervalMinutes must be between 1 and 1440.")]
    public System.Int32 DeepTrimIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Enable buffer usage analytics to optimize allocation strategy.
    /// </summary>
    [IniComment("Collect usage analytics to optimize allocation strategy over time")]
    public System.Boolean EnableAnalytics { get; set; } = false;

    /// <summary>
    /// Adaptive growth factor for high-demand buffer sizes.
    /// </summary>
    [IniComment("Multiplier for pool expansion under high demand (1.25–4.0)")]
    [System.ComponentModel.DataAnnotations.Range(1.25, 4.0, ErrorMessage = "AdaptiveGrowthFactor must be in range [1.25, 4.0].")]
    public System.Double AdaptiveGrowthFactor { get; set; } = 2.0;

    /// <summary>
    /// Maximum percentage of system memory to use for buffer pools.
    /// </summary>
    [IniComment("Maximum fraction of system memory for buffer pools (0–0.90)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.Double), "0.000001", "0.90", ErrorMessage = "MaxMemoryPercentage must be in (0, 0.90].")]
    public System.Double MaxMemoryPercentage { get; set; } = 0.25;

    /// <summary>
    /// Enable zero-memory clear on buffer return for security-sensitive applications.
    /// </summary>
    [IniComment("Zero-fill buffers on return to prevent data leakage (impacts performance)")]
    public System.Boolean SecureClear { get; set; } = false;

    /// <summary>
    /// Enable queue compaction to reduce memory fragmentation.
    /// </summary>
    [IniComment("Compact internal queues to reduce memory fragmentation")]
    public System.Boolean EnableQueueCompaction { get; set; } = false;

    /// <summary>
    /// The number of buffer rent/return operations between auto-tuning cycles.
    /// </summary>
    [IniComment("Rent/return operations between auto-tune cycles (minimum 10, should be >= TotalBuffers)")]
    [System.ComponentModel.DataAnnotations.Range(10, System.Int32.MaxValue, ErrorMessage = "AutoTuneOperationThreshold must be >= 10.")]
    public System.Int32 AutoTuneOperationThreshold { get; set; } = 10_000;

    /// <summary>
    /// Whether to fall back to <see cref="System.Buffers.ArrayPool{T}.Shared"/> when no suitable pool exists.
    /// </summary>
    [IniComment("Fall back to ArrayPool.Shared when no pool matches the requested size")]
    public System.Boolean FallbackToArrayPool { get; set; } = true;

    /// <summary>
    /// Free/Total ratio threshold to trigger expansion.
    /// </summary>
    [IniComment("Free/Total ratio below which a pool expands (must be less than ShrinkThresholdPercent)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.Double), "0.000001", "0.999999", ErrorMessage = "ExpandThresholdPercent must be in (0,1).")]
    public System.Double ExpandThresholdPercent { get; set; } = 0.25;

    /// <summary>
    /// Free/Total ratio threshold to allow shrink.
    /// </summary>
    [IniComment("Free/Total ratio above which a pool shrinks (must be greater than ExpandThresholdPercent)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.Double), "0.000001", "0.999999", ErrorMessage = "ShrinkThresholdPercent must be in (0,1).")]
    public System.Double ShrinkThresholdPercent { get; set; } = 0.50;

    /// <summary>
    /// Minimum increase step when growing a pool.
    /// </summary>
    [IniComment("Minimum number of buffers added per expansion step (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "MinimumIncrease must be at least 1.")]
    public System.Int32 MinimumIncrease { get; set; } = 4;

    /// <summary>
    /// Maximum one-shot buffer increase to cap memory spikes.
    /// </summary>
    [IniComment("Maximum buffers added in a single expansion to prevent memory spikes (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "MaxBufferIncreaseLimit must be at least 1.")]
    public System.Int32 MaxBufferIncreaseLimit { get; set; } = 1024;

    /// <summary>
    /// Semicolon-separated list of buffer size and ratio pairs. Example: "1024,0.40; 2048,0.25".
    /// </summary>
    [IniComment("Semicolon-separated size,ratio pairs for pool allocation (e.g. 1024,0.25; 4096,0.15)\nSizes must be strictly increasing and ratios must sum to <= 1.0")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "BufferAllocations is required.")]
    [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "BufferAllocations cannot be empty.")]
    public System.String BufferAllocations { get; set; } = "256,0.10; 512,0.15; 1024,0.20; 2048,0.20; 4096,0.15; 8192,0.10; 16384,0.10";

    /// <summary>
    /// Maximum memory in bytes that buffer pools can use. 0 means no limit.
    /// </summary>
    [IniComment("Hard memory cap for all buffer pools in bytes (0 = no limit)")]
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
        ValidationContext context = new System.ComponentModel.DataAnnotations.ValidationContext(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);

        if (ExpandThresholdPercent >= ShrinkThresholdPercent)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                "ExpandThresholdPercent must be less than ShrinkThresholdPercent.");
        }

        try
        {
            (System.Int32, System.Double)[] allocations = ParseBufferAllocations(BufferAllocations);

            System.Double totalRatio = 0;
            System.Int32 lastSize = 0;

            foreach ((System.Int32 size, System.Double ratio) in allocations)
            {
                if (size > lastSize)
                {
                    totalRatio += ratio;
                    lastSize = size;
                    continue;
                }

                throw new System.ComponentModel.DataAnnotations.ValidationException(
                    $"BufferAllocations sizes must be strictly increasing (got {lastSize} then {size}).");
            }

            if (totalRatio > 1.01)
            {
                throw new System.ComponentModel.DataAnnotations.ValidationException(
                    $"Sum of buffer allocation ratios exceeds 1.0 ({totalRatio}).");
            }
        }
        catch (System.Exception ex)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"Invalid BufferAllocations: {ex.Message}");
        }

        if (MaxMemoryBytes > 0 && MaxMemoryPercentage > 0.90)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                "Cannot specify both MaxMemoryBytes and MaxMemoryPercentage > 0.90.");
        }

        if (AdaptiveGrowthFactor * MinimumIncrease > MaxBufferIncreaseLimit)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                "AdaptiveGrowthFactor * MinimumIncrease must be <= MaxBufferIncreaseLimit.");
        }

        if (AutoTuneOperationThreshold < TotalBuffers)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                "AutoTuneOperationThreshold should normally be >= TotalBuffers.");
        }
    }

    #region Parsing

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.String, (System.Int32, System.Double)[]> _allocationPatternCache = new();

    /// <summary>
    /// Parses the buffer allocation settings with caching for repeated configurations.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static (System.Int32, System.Double)[] ParseBufferAllocations(System.String bufferAllocationsString)
    {
        return System.String.IsNullOrWhiteSpace(bufferAllocationsString)
            ? throw new System.ArgumentException(
                $"[{nameof(BufferConfig)}] The input string must not be blank.", nameof(bufferAllocationsString))
            : _allocationPatternCache.GetOrAdd(bufferAllocationsString, key =>
            {
                try
                {
                    (System.Int32 allocationSize, System.Double ratio)[] allocations = PARSE_ALLOCATIONS(key, bufferAllocationsString);
                    System.Double totalAllocation = System.Linq.Enumerable.Sum(allocations, a => a.ratio);
                    return totalAllocation > 1.1
                        ? throw new System.ArgumentException(
                            $"[{nameof(BufferConfig)}] Total allocation ratio ({totalAllocation:F2}) exceeds 1.0.")
                        : ((System.Int32, System.Double)[])allocations;
                }
                catch (System.Exception ex) when (ex is System.FormatException or System.ArgumentException
                                                      or System.OverflowException or System.ArgumentOutOfRangeException)
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
    private static (System.Int32 allocationSize, System.Double ratio)[] PARSE_ALLOCATIONS(
        System.String key, System.String bufferAllocationsString)
    {
        System.String[] pairs = key.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
        List<(int, double)> list = new System.Collections.Generic.List<(System.Int32, System.Double)>();

        foreach (System.String pair in pairs)
        {
            System.String[] parts = pair.Split(',', System.StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new System.FormatException($"[{nameof(BufferConfig)}] Incorrectly formatted pair: '{pair}'.");
            }

            if (!System.Int32.TryParse(parts[0].Trim(), out System.Int32 allocationSize) || allocationSize <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(bufferAllocationsString), $"[{nameof(BufferConfig)}] SIZE must be > 0.");
            }

            if (!System.Double.TryParse(parts[1].Trim(), out System.Double ratio) || ratio <= 0 || ratio > 1)
            {
                throw new System.ArgumentOutOfRangeException(nameof(bufferAllocationsString), $"[{nameof(BufferConfig)}] Ratio must be (0,1].");
            }

            list.Add((allocationSize, ratio));
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return [.. list];
    }

    #endregion Parsing
}
