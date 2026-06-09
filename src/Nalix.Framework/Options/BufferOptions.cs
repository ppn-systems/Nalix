// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Configuration for buffer settings with validation and performance/security options.
/// </summary>
[IniComment("Buffer pool configuration — controls pool sizing, trimming, adaptive growth, and memory limits")]
public sealed partial class BufferOptions : ConfigurationLoader, IValidatableConfiguration
{
    #region Properties
    /// <summary>
    /// The total number of buffers to create across all pools.
    /// </summary>
    [IniComment("Total buffers to create across all pools (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int TotalBuffers { get; set; } = 16_384;

    /// <summary>
    /// Enables memory trimming to periodically recover unused buffers.
    /// </summary>
    [IniComment("Periodically return unused buffers to reclaim memory")]
    public bool EnableMemoryTrimming { get; set; } = true;

    /// <summary>
    /// Time interval in minutes between memory trimming operations.
    /// </summary>
    [IniComment("Interval in minutes between light trim cycles (1–60)")]
    [ValueRange(1, 60)]
    public int TrimIntervalMinutes { get; set; } = 2;

    /// <summary>
    /// Time interval in minutes for deep trimming operations.
    /// </summary>
    [IniComment("Interval in minutes between deep trim cycles (1–1440)")]
    [ValueRange(1, 1440)]
    public int DeepTrimIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Enable buffer usage analytics to optimize allocation strategy.
    /// </summary>
    [IniComment("Collect usage analytics to optimize allocation strategy over time")]
    public bool EnableAnalytics { get; set; } = false;

    /// <summary>
    /// Adaptive growth factor for high-demand buffer sizes.
    /// </summary>
    [IniComment("Multiplier for pool expansion under high demand (1.25–4.0)")]
    [ValueRange(1.25, 4.0)]
    public double AdaptiveGrowthFactor { get; set; } = 2.0;

    /// <summary>
    /// Maximum percentage of system memory to use for buffer pools.
    /// </summary>
    [IniComment("Maximum fraction of system memory for buffer pools (0–0.90)")]
    [ValueRange(0.000001, 0.90)]
    public double MaxMemoryPercentage { get; set; } = 0.25;

    /// <summary>
    /// Maximum depth for the per-thread buffer cache.
    /// WARNING: Do not enable (keep at 0) in highly asynchronous environments (async/await) 
    /// using the ThreadPool, as it will lead to memory hoarding on threads and inaccurate metrics.
    /// </summary>
    [IniComment("Max buffers held in thread-local cache. Keep at 0 (disabled) for async/await ThreadPool workloads to prevent memory hoarding.")]
    public int ThreadCacheDepth { get; set; } = 0;

    /// <summary>
    /// Maximum size of the internal suitable pool size cache.
    /// </summary>
    [IniComment("Max entries in the suitable size lookup cache")]
    [ValueRange(100, 5000)]
    public int SuitablePoolSizeCacheLimit { get; set; } = 1000;

    /// <summary>
    /// Whether to fall back to <see cref="System.Buffers.ArrayPool{T}.Shared"/> when no suitable pool exists.
    /// </summary>
    [IniComment("Fall back to ArrayPool.Shared when no pool matches the requested size")]
    public bool FallbackToArrayPool { get; set; } = true;

    /// <summary>
    /// Free/Total ratio threshold to trigger expansion.
    /// </summary>
    [IniComment("Free/Total ratio below which a pool expands (must be less than ShrinkThresholdPercent)")]
    [ValueRange(0.000001, 0.999999)]
    public double ExpandThresholdPercent { get; set; } = 0.35;

    /// <summary>
    /// Free/Total ratio threshold to allow shrink.
    /// </summary>
    [IniComment("Free/Total ratio above which a pool shrinks (must be greater than ExpandThresholdPercent)")]
    [ValueRange(0.000001, 0.999999)]
    public double ShrinkThresholdPercent { get; set; } = 0.60;

    /// <summary>
    /// Minimum increase step when growing a pool.
    /// </summary>
    [IniComment("Minimum number of buffers added per expansion step (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int MinimumIncrease { get; set; } = 128;

    /// <summary>
    /// Maximum one-shot buffer increase to cap memory spikes.
    /// </summary>
    [IniComment("Maximum buffers added in a single expansion to prevent memory spikes (minimum 1)")]
    [ValueRange(1, int.MaxValue)]
    public int MaxBufferIncreaseLimit { get; set; } = 2048;

    /// <summary>
    /// Semicolon-separated list of buffer size and ratio pairs. Example: "1024,0.40; 2048,0.25".
    /// </summary>
    [IniComment("Semicolon-separated size,ratio pairs for pool allocation (e.g. 1024,0.25; 4096,0.15)\nSizes must be strictly increasing and ratios must sum to <= 1.0")]
    [Length(1)]
    
    public string BufferAllocations { get; set; } = "256,0.15; 1024,0.15; 4096,0.30; 16384,0.30; 32768,0.10";

    /// <summary>
    /// Maximum memory in bytes that buffer pools can use. 0 means no limit.
    /// </summary>
    [IniComment("Hard memory cap for all buffer pools in bytes (0 = no limit)")]
    [ValueRange(0, long.MaxValue)]
    public long MaxMemoryBytes { get; set; } = 0;

    /// <summary>
    /// Enables GC-based leak detection using finalizers.
    /// When enabled, a sentinel is attached to rented objects to report if they are GC'd without being returned.
    /// </summary>
    [IniComment("Enable GC-based leak detection using sentinel finalizers")]
    public bool EnableBufferLeakDetection { get; set; } = false;

    /// <summary>
    /// Enable capturing stack traces for buffer leaks.
    /// Capturing stack trace is extremely expensive and should be disabled
    /// during high-concurrency benchmarks.
    /// </summary>
    [IniComment("Enable expensive stack trace capture for buffer leak detection")]
    public bool EnableBufferLeakStackTrace { get; set; } = false;

    /// <summary>
    /// Multiplier for usage-based growth acceleration.
    /// </summary>
    [IniComment("Aggressiveness of growth based on usage (higher = grows faster under load)")]
    public double UsageAggressiveFactor { get; set; } = 0.75;

    /// <summary>
    /// Multiplier for miss-rate based growth acceleration.
    /// </summary>
    [IniComment("Aggressiveness of growth based on misses")]
    public double MissRateAggressiveFactor { get; set; } = 2.0;

    /// <summary>
    /// Soft cap ratio for pool expansion relative to current total buffers.
    /// </summary>
    [IniComment("Max expansion step as fraction of current pool size")]
    public double ExpansionSoftCapRatio { get; set; } = 0.25;

    /// <summary>
    /// Initial internal capacity for the slab tracking dictionary.
    /// </summary>
    [IniComment("Initial capacity for internal slab tracking (power of 2)")]
    public int InitialSlabTrackingCapacity { get; set; } = 128;

    /// <summary>
    /// Threshold in seconds after which an outstanding object is considered "suspicious".
    /// </summary>
    [IniComment("Threshold in seconds to flag 'suspicious' objects in reports")]
    [ValueRange(0, 3600)]
    public int SuspiciousThresholdSeconds { get; set; } = 30;

    #endregion Properties

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="Nalix.Abstractions.Validation.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown by <see cref="ParseBufferAllocations(string)"/> and wrapped into a validation failure when the allocation pattern is malformed.
    /// </exception>
    public void Validate()
    {
        this.ValidateDataAnnotations();

        if (this.ExpandThresholdPercent >= this.ShrinkThresholdPercent)
        {
            throw new global::Nalix.Abstractions.Validation.ValidationException(
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

                throw new global::Nalix.Abstractions.Validation.ValidationException(
                    $"BufferAllocations sizes must be strictly increasing (got {lastSize} then {size}).");
            }

            if (totalRatio > 1.01)
            {
                throw new global::Nalix.Abstractions.Validation.ValidationException(
                    $"Sum of buffer allocation ratios exceeds 1.0 ({totalRatio}).");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            throw new global::Nalix.Abstractions.Validation.ValidationException(
                $"Invalid BufferAllocations: {ex.Message}");
        }

        if (this.MaxMemoryBytes > 0 && this.MaxMemoryPercentage > 0.90)
        {
            throw new global::Nalix.Abstractions.Validation.ValidationException(
                "Cannot specify both MaxMemoryBytes and MaxMemoryPercentage > 0.90.");
        }

        if (this.AdaptiveGrowthFactor * this.MinimumIncrease > this.MaxBufferIncreaseLimit)
        {
            throw new global::Nalix.Abstractions.Validation.ValidationException(
                "AdaptiveGrowthFactor * MinimumIncrease must be <= MaxBufferIncreaseLimit.");
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
                $"[{nameof(BufferOptions)}] The input string must not be blank.", nameof(bufferAllocationsString))
            : s_allocationPatternCache.GetOrAdd(bufferAllocationsString, key =>
            {
                try
                {
                    (int allocationSize, double ratio)[] allocations = PARSE_ALLOCATIONS(key, bufferAllocationsString);
                    double totalAllocation = Enumerable.Sum(allocations, a => a.ratio);
                    return totalAllocation > 1.1
                        ? throw new ArgumentException(
                            $"[{nameof(BufferOptions)}] Total allocation ratio ({totalAllocation:F2}) exceeds 1.0.")
                        : ((int, double)[])allocations;
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException
                                            or OverflowException or ArgumentOutOfRangeException)
                {
                    throw new ArgumentException(
                        $"[{nameof(BufferOptions)}] Malformed allocation string. Expected '<size>,<ratio>;...'. ERROR: {ex.Message}");
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
                throw new FormatException($"[{nameof(BufferOptions)}] Incorrectly formatted pair: '{pair}'.");
            }

            if (!int.TryParse(parts[0].Trim(), out int allocationSize) || allocationSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferAllocationsString), $"[{nameof(BufferOptions)}] SIZE must be > 0.");
            }

            if (!double.TryParse(parts[1].Trim(), out double ratio) || ratio <= 0 || ratio > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferAllocationsString), $"[{nameof(BufferOptions)}] Ratio must be (0,1].");
            }

            list.Add((allocationSize, ratio));
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return [.. list];
    }

    #endregion Parsing
}
