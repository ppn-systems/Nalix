// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Options;

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
    [Range(1, int.MaxValue, ErrorMessage = "TotalBuffers must be greater than 0.")]
    public int TotalBuffers { get; set; } = 16384;

    /// <summary>
    /// Enables memory trimming to periodically recover unused buffers.
    /// </summary>
    [IniComment("Periodically return unused buffers to reclaim memory")]
    public bool EnableMemoryTrimming { get; set; } = true;

    /// <summary>
    /// Time interval in minutes between memory trimming operations.
    /// </summary>
    [IniComment("Interval in minutes between light trim cycles (1–60)")]
    [Range(1, 60, ErrorMessage = "TrimIntervalMinutes must be between 1 and 60.")]
    public int TrimIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Time interval in minutes for deep trimming operations.
    /// </summary>
    [IniComment("Interval in minutes between deep trim cycles (1–1440)")]
    [Range(1, 1440, ErrorMessage = "DeepTrimIntervalMinutes must be between 1 and 1440.")]
    public int DeepTrimIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Enable buffer usage analytics to optimize allocation strategy.
    /// </summary>
    [IniComment("Collect usage analytics to optimize allocation strategy over time")]
    public bool EnableAnalytics { get; set; } = false;

    /// <summary>
    /// Adaptive growth factor for high-demand buffer sizes.
    /// </summary>
    [IniComment("Multiplier for pool expansion under high demand (1.25–4.0)")]
    [Range(1.25, 4.0, ErrorMessage = "AdaptiveGrowthFactor must be in range [1.25, 4.0].")]
    public double AdaptiveGrowthFactor { get; set; } = 2.0;

    /// <summary>
    /// Maximum percentage of system memory to use for buffer pools.
    /// </summary>
    [IniComment("Maximum fraction of system memory for buffer pools (0–0.90)")]
    [Range(typeof(double), "0.000001", "0.90", ErrorMessage = "MaxMemoryPercentage must be in (0, 0.90].")]
    public double MaxMemoryPercentage { get; set; } = 0.25;

    /// <summary>
    /// Enable queue compaction to reduce memory fragmentation.
    /// </summary>
    [IniComment("Compact internal queues to reduce memory fragmentation")]
    public bool EnableQueueCompaction { get; set; } = false;

    /// <summary>
    /// The number of buffer rent/return operations between auto-tuning cycles.
    /// </summary>
    [IniComment("Rent/return operations between auto-tune cycles (minimum 10, should be >= TotalBuffers)")]
    [Range(10, int.MaxValue, ErrorMessage = "AutoTuneOperationThreshold must be >= 10.")]
    public int AutoTuneOperationThreshold { get; set; } = 20_000;

    /// <summary>
    /// Whether to fall back to <see cref="System.Buffers.ArrayPool{T}.Shared"/> when no suitable pool exists.
    /// </summary>
    [IniComment("Fall back to ArrayPool.Shared when no pool matches the requested size")]
    public bool FallbackToArrayPool { get; set; } = true;

    /// <summary>
    /// Free/Total ratio threshold to trigger expansion.
    /// </summary>
    [IniComment("Free/Total ratio below which a pool expands (must be less than ShrinkThresholdPercent)")]
    [Range(typeof(double), "0.000001", "0.999999", ErrorMessage = "ExpandThresholdPercent must be in (0,1).")]
    public double ExpandThresholdPercent { get; set; } = 0.35;

    /// <summary>
    /// Free/Total ratio threshold to allow shrink.
    /// </summary>
    [IniComment("Free/Total ratio above which a pool shrinks (must be greater than ExpandThresholdPercent)")]
    [Range(typeof(double), "0.000001", "0.999999", ErrorMessage = "ShrinkThresholdPercent must be in (0,1).")]
    public double ShrinkThresholdPercent { get; set; } = 0.60;

    /// <summary>
    /// Minimum increase step when growing a pool.
    /// </summary>
    [IniComment("Minimum number of buffers added per expansion step (minimum 1)")]
    [Range(1, int.MaxValue, ErrorMessage = "MinimumIncrease must be at least 1.")]
    public int MinimumIncrease { get; set; } = 128;

    /// <summary>
    /// Maximum one-shot buffer increase to cap memory spikes.
    /// </summary>
    [IniComment("Maximum buffers added in a single expansion to prevent memory spikes (minimum 1)")]
    [Range(1, int.MaxValue, ErrorMessage = "MaxBufferIncreaseLimit must be at least 1.")]
    public int MaxBufferIncreaseLimit { get; set; } = 2048;

    /// <summary>
    /// Semicolon-separated list of buffer size and ratio pairs. Example: "1024,0.40; 2048,0.25".
    /// </summary>
    [IniComment("Semicolon-separated size,ratio pairs for pool allocation (e.g. 1024,0.25; 4096,0.15)\nSizes must be strictly increasing and ratios must sum to <= 1.0")]
    [Required(ErrorMessage = "BufferAllocations is required.")]
    [MinLength(1, ErrorMessage = "BufferAllocations cannot be empty.")]
    public string BufferAllocations { get; set; } = "256,0.15; 512,0.10; 1024,0.10; 2048,0.10; 4096,0.10; 8192,0.10; 16384,0.35";

    /// <summary>
    /// Maximum memory in bytes that buffer pools can use. 0 means no limit.
    /// </summary>
    [IniComment("Hard memory cap for all buffer pools in bytes (0 = no limit)")]
    [Range(0, long.MaxValue, ErrorMessage = "MaxMemoryBytes cannot be negative.")]
    public long MaxMemoryBytes { get; set; } = 0;

    #endregion Properties

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown by <see cref="ParseBufferAllocations(string)"/> and wrapped into a validation failure when the allocation pattern is malformed.
    /// </exception>
    public void Validate()
    {
        ValidationContext context = new(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);

        if (this.ExpandThresholdPercent >= this.ShrinkThresholdPercent)
        {
            throw new ValidationException(
                "ExpandThresholdPercent must be less than ShrinkThresholdPercent.");
        }

        try
        {
            (int, double)[] allocations = ParseBufferAllocations(this.BufferAllocations);

            double totalRatio = 0;
            int lastSize = 0;

            foreach ((int size, double ratio) in allocations)
            {
                if (size > lastSize)
                {
                    totalRatio += ratio;
                    lastSize = size;
                    continue;
                }

                throw new ValidationException(
                    $"BufferAllocations sizes must be strictly increasing (got {lastSize} then {size}).");
            }

            if (totalRatio > 1.01)
            {
                throw new ValidationException(
                    $"Sum of buffer allocation ratios exceeds 1.0 ({totalRatio}).");
            }
        }
        catch (Exception ex)
        {
            throw new ValidationException(
                $"Invalid BufferAllocations: {ex.Message}");
        }

        if (this.MaxMemoryBytes > 0 && this.MaxMemoryPercentage > 0.90)
        {
            throw new ValidationException(
                "Cannot specify both MaxMemoryBytes and MaxMemoryPercentage > 0.90.");
        }

        if (this.AdaptiveGrowthFactor * this.MinimumIncrease > this.MaxBufferIncreaseLimit)
        {
            throw new ValidationException(
                "AdaptiveGrowthFactor * MinimumIncrease must be <= MaxBufferIncreaseLimit.");
        }

        if (this.AutoTuneOperationThreshold < this.TotalBuffers)
        {
            throw new ValidationException(
                "AutoTuneOperationThreshold should normally be >= TotalBuffers.");
        }
    }

    #region Parsing

    private static readonly ConcurrentDictionary<string, (int, double)[]> s_allocationPatternCache = new();

    /// <summary>
    /// Parses the buffer allocation settings with caching for repeated configurations.
    /// </summary>
    /// <param name="bufferAllocationsString">Semicolon-separated <c>size,ratio</c> pairs.</param>
    /// <returns>The parsed allocation pairs sorted by allocation size.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bufferAllocationsString"/> is blank or malformed.</exception>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static (int, double)[] ParseBufferAllocations(string bufferAllocationsString)
    {
        return string.IsNullOrWhiteSpace(bufferAllocationsString)
            ? throw new ArgumentException(
                $"[{nameof(BufferConfig)}] The input string must not be blank.", nameof(bufferAllocationsString))
            : s_allocationPatternCache.GetOrAdd(bufferAllocationsString, key =>
            {
                try
                {
                    (int allocationSize, double ratio)[] allocations = PARSE_ALLOCATIONS(key, bufferAllocationsString);
                    double totalAllocation = Enumerable.Sum(allocations, a => a.ratio);
                    return totalAllocation > 1.1
                        ? throw new ArgumentException(
                            $"[{nameof(BufferConfig)}] Total allocation ratio ({totalAllocation:F2}) exceeds 1.0.")
                        : ((int, double)[])allocations;
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException
                                                      or OverflowException or ArgumentOutOfRangeException)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[SH.{nameof(BufferConfig)}:Internal] " +
                                                   $"alloc-parse-fail str='{bufferAllocationsString}' msg={ex.Message}");

                    throw new ArgumentException(
                        $"[{nameof(BufferConfig)}] Malformed allocation string. Expected '<size>,<ratio>;...'. ERROR: {ex.Message}");
                }
            });
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (int allocationSize, double ratio)[] PARSE_ALLOCATIONS(string key, string bufferAllocationsString)
    {
        string[] pairs = key.Split(';', StringSplitOptions.RemoveEmptyEntries);
        List<(int, double)> list = [];

        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                throw new FormatException($"[{nameof(BufferConfig)}] Incorrectly formatted pair: '{pair}'.");
            }

            if (!int.TryParse(parts[0].Trim(), out int allocationSize) || allocationSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferAllocationsString), $"[{nameof(BufferConfig)}] SIZE must be > 0.");
            }

            if (!double.TryParse(parts[1].Trim(), out double ratio) || ratio <= 0 || ratio > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferAllocationsString), $"[{nameof(BufferConfig)}] Ratio must be (0,1].");
            }

            list.Add((allocationSize, ratio));
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return [.. list];
    }

    #endregion Parsing
}
