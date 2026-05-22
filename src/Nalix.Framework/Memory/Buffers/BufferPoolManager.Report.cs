// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Internal.Buffers;

namespace Nalix.Framework.Memory.Buffers;

public sealed partial class BufferPoolManager
{
    /// <summary>
    /// Generates a report on the current state of the buffer pools with metrics.
    /// The text report is meant for humans: it summarizes configuration,
    /// capacities, and live usage in one place.
    /// </summary>
    /// <returns>A string containing the report.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new();

        this.APPEND_REPORT_HEADER(sb);
        this.APPEND_SUSPICIOUS_BUFFERS(sb);
        this.APPEND_REPORT_POOL_DETAILS(sb);
        this.APPEND_REPORT_METRICS(sb);

        return sb.ToString();
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteBoolean("Initialized", _isInitialized);
        writer.WriteNumber("TotalBuffersConfigured", _config.TotalBuffers);
        writer.WriteNumber("PoolCount", _bufferAllocations.Length);
        writer.WriteNumber(nameof(this.MinBufferSize), this.MinBufferSize);
        writer.WriteNumber(nameof(this.MaxBufferSize), this.MaxBufferSize);
        writer.WriteBoolean("EnableTrimming", _config.EnableMemoryTrimming);
        writer.WriteBoolean("EnableAnalytics", _config.EnableAnalytics);
        writer.WriteBoolean("FallbackToArrayPool", _config.FallbackToArrayPool);
        writer.WriteNumber("TrimIntervalMinutes", _config.TrimIntervalMinutes);
        writer.WriteNumber("DeepTrimIntervalMinutes", _config.DeepTrimIntervalMinutes);
        writer.WriteNumber("TrimCycleCount", _trimCycleCount);
        writer.WriteNumber("FallbackCount", _fallbackCount);
        writer.WriteNumber("BucketCacheHits", _suitablePoolSizeCacheHits);
        writer.WriteNumber("BucketCacheMisses", _suitablePoolSizeCacheMisses);
        writer.WriteNumber("PeakMemoryUsageBytes", _peakMemoryUsage);
        writer.WriteNumber("ThroughputMBps", (DateTime.UtcNow - _startTime).TotalSeconds > 0
            ? (double)this.GetTotalBytesRented() / (1024 * 1024) / (DateTime.UtcNow - _startTime).TotalSeconds
            : 0);

        writer.WriteStartObject("ShrinkSafetyPolicy");
        writer.WriteNumber("MinimumRetentionPercent", _shrinkPolicy.MinimumRetentionPercent);
        writer.WriteNumber("MaxSingleShrinkStep", _shrinkPolicy.MaxSingleShrinkStep);
        writer.WriteNumber("MaxShrinkPercentPerCycle", _shrinkPolicy.MaxShrinkPercentPerCycle);
        writer.WriteNumber("AbsoluteMinimum", _shrinkPolicy.AbsoluteMinimum);
        writer.WriteEndObject();

        IReadOnlyCollection<SlabBucket> allBuckets = _slabPool.GetAllBuckets();

        long totalHits = 0;
        long totalMisses = 0;
        long totalExpands = 0;
        long totalShrinks = 0;

        writer.WriteStartArray("Pools");
        foreach (SlabBucket bucket in allBuckets)
        {
            BufferPoolState info = bucket.GetPoolInfo();
            totalHits += info.Hits;
            totalMisses += info.Misses;
            totalExpands += info.Expands;
            totalShrinks += info.Shrinks;

            int inUse = info.TotalBuffers - info.FreeBuffers;
            double usage = info.GetUsageRatio();
            double miss = info.GetMissRate();

            _ = _metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics);

            string bytesReturned = metrics.TotalBytesReturned > 1_000_000
                ? $"{metrics.TotalBytesReturned / 1_000_000}MB"
                : $"{metrics.TotalBytesReturned / 1024}KB";

            writer.WriteStartObject();
            writer.WriteNumber("BufferSize", info.BufferSize);
            writer.WriteNumber("Initial", info.InitialCapacity);
            writer.WriteNumber("Total", info.TotalBuffers);
            writer.WriteNumber("Free", info.FreeBuffers);
            writer.WriteNumber("InUse", inUse);
            writer.WriteNumber("Hits", info.Hits);
            writer.WriteNumber("Expands", info.Expands);
            writer.WriteNumber("Shrinks", info.Shrinks);
            writer.WriteNumber("UsageRatio", usage);
            writer.WriteNumber("MissRate", miss);
            writer.WriteNumber("ShrinkSkipped", metrics.ShrinkSkipped);
            writer.WriteString("BytesReturned", bytesReturned);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteNumber("TotalHits", totalHits);
        writer.WriteNumber("TotalMisses", totalMisses);
        writer.WriteNumber("TotalExpands", totalExpands);
        writer.WriteNumber("TotalShrinks", totalShrinks);
        writer.WriteNumber("HitRate", (totalHits + totalMisses) > 0 ? (double)totalHits / (totalHits + totalMisses) : 1.0);

        writer.WriteEndObject();
    }


    #region Private: Reporting

    private void APPEND_SUSPICIOUS_BUFFERS(StringBuilder sb)
    {
        if (!_config.EnableBufferLeakDetection)
        {
            return;
        }

        _ = sb.AppendLine("Suspicious Buffers (Outstanding > " + _config.SuspiciousThresholdSeconds + "s):");
        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");
        _ = sb.AppendLine("SIZE (bytes) | Elapsed (s) | Stack Trace (first line)");
        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");

        long now = Stopwatch.GetTimestamp();
        long thresholdTicks = _config.SuspiciousThresholdSeconds * Stopwatch.Frequency;
        int found = 0;

        List<WeakReference<BufferSentinel>> survivors = new();

        foreach (WeakReference<BufferSentinel> weakRef in _sentinelTracker)
        {
            if (weakRef.TryGetTarget(out BufferSentinel? sentinel))
            {
                if (sentinel.IsReturned)
                {
                    continue;
                }

                survivors.Add(weakRef);

                long elapsed = now - sentinel.RentTimestamp;
                if (elapsed >= thresholdTicks)
                {
                    found++;
                    double elapsedSec = elapsed / (double)Stopwatch.Frequency;

                    string stack = "N/A (CaptureStackTrace=false)";
                    if (!string.IsNullOrEmpty(sentinel.StackTrace))
                    {
                        int firstLineEnd = sentinel.StackTrace.IndexOf('\n', StringComparison.Ordinal);
                        stack = firstLineEnd > 0
                            ? sentinel.StackTrace[..firstLineEnd].Trim()
                            : sentinel.StackTrace;
                    }

                    if (found <= 20)
                    {
                        _ = sb.AppendLine(CultureInfo.InvariantCulture,
                            $"{sentinel.Size,12} | {elapsedSec,11:F1} | {stack}");
                    }
                }
            }
        }

        if (_sentinelTracker.Count > 10000 && survivors.Count < _sentinelTracker.Count * 0.7)
        {
            ConcurrentBag<WeakReference<BufferSentinel>> newBag = new();
            foreach (WeakReference<BufferSentinel> wr in survivors)
            {
                newBag.Add(wr);
            }
            _sentinelTracker = newBag;
        }

        if (found > 20)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture,
                $"... and {found - 20} more suspicious buffers.");
        }

        if (found == 0)
        {
            _ = sb.AppendLine("(None detected)");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------------------------------");
        _ = sb.AppendLine();
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_HEADER(StringBuilder sb)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] BufferPoolManager Status:");
        _ = sb.AppendLine();

        _ = sb.AppendLine("======================================================================");
        _ = sb.AppendLine("Overall Statistics");
        _ = sb.AppendLine("======================================================================");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Initialized               : {_isInitialized}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Buffers (Configured): {_config.TotalBuffers}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Pools                     : {_bufferAllocations.Length}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Min Buffer SIZE           : {this.MinBufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Max Buffer SIZE           : {this.MaxBufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Enable Trimming           : {_config.EnableMemoryTrimming}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Enable Analytics          : {_config.EnableAnalytics}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Management Capacity : {_config.TotalBuffers}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Fallback to ArrayPool     : {_config.FallbackToArrayPool}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Trim Interval (min)       : {_config.TrimIntervalMinutes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Deep Trim Interval (min)  : {_config.DeepTrimIntervalMinutes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Trim Cycles Run           : {_trimCycleCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Fallback (ArrayPool)      : {_fallbackCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Bucket Cache Hits         : {_suitablePoolSizeCacheHits}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Bucket Cache Miss         : {_suitablePoolSizeCacheMisses}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Shrink Safety Policy:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Minimum Retention       : {_shrinkPolicy.MinimumRetentionPercent * 100:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Max Single Shrink Step  : {_shrinkPolicy.MaxSingleShrinkStep}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Max Shrink Per Cycle    : {_shrinkPolicy.MaxShrinkPercentPerCycle * 100:F1}%");
        _ = sb.AppendLine();
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_POOL_DETAILS(StringBuilder sb)
    {
        _ = sb.AppendLine("============================================================================");
        _ = sb.AppendLine("Buffer Details (Dashboard):");
        _ = sb.AppendLine("============================================================================");
        _ = sb.AppendLine("SIZE     | CAPACITY (F/T/I)         | OPS (H/E/S)         | USAGE % | MISS %");
        _ = sb.AppendLine("---------+--------------------------+---------------------+---------+-------");

        List<SlabBucket> buckets = [.. _slabPool.GetAllBuckets()];
        buckets.Sort(static (a, b) => a.GetPoolInfo().BufferSize.CompareTo(b.GetPoolInfo().BufferSize));

        long totalHits = 0;
        long totalMisses = 0;
        long totalExpands = 0;
        long totalShrinks = 0;

        foreach (SlabBucket bucket in buckets)
        {
            BufferPoolState info = bucket.GetPoolInfo();
            totalHits += info.Hits;
            totalMisses += info.Misses;
            totalExpands += info.Expands;
            totalShrinks += info.Shrinks;

            double usage = info.GetUsageRatio() * 100.0;
            double miss = info.GetMissRate() * 100.0;

            string capacity = $"{info.FreeBuffers.FormatCompact()} / {info.TotalBuffers.FormatCompact()} / {info.InitialCapacity.FormatCompact()}";
            string ops = $"{info.Hits.FormatCompact()} / {info.Expands.FormatCompact()} / {info.Shrinks.FormatCompact()}";

            _ = sb.AppendLine(CultureInfo.InvariantCulture,
                $"{info.BufferSize,8} | {capacity,-24} | {ops,-19} | {usage,6:F2}% | {miss:F2}%");
        }


        double hitRate = (totalHits + totalMisses) > 0 ? (double)totalHits / (totalHits + totalMisses) : 1.0;

        _ = sb.AppendLine("----------------------------------------------------------------------------");
        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Hits           : {totalHits}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Misses         : {totalMisses}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Hit Rate       : {hitRate * 100:F2}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Expands        : {totalExpands}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Shrinks        : {totalShrinks}");

        double uptimeSec = (DateTime.UtcNow - _startTime).TotalSeconds;
        double throughputMBps = uptimeSec > 0 ? (double)this.GetTotalBytesRented() / (1024 * 1024) / uptimeSec : 0;
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Throughput           : {throughputMBps:F2} MB/s");

        long currentMem = 0;
        foreach (SlabBucket bucket in _slabPool.GetAllBuckets())
        {
            BufferPoolState info = bucket.GetPoolInfo();
            currentMem += (long)info.TotalBuffers * info.BufferSize;
        }

        (long targetBudget, long _, bool _) = this.COMPUTE_MEMORY_BUDGET();
        double budgetUsage = targetBudget > 0 ? (double)currentMem / targetBudget * 100.0 : 0;

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Peak Memory (POH)    : {Volatile.Read(ref _peakMemoryUsage) / 1048576:N0} MB");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Current Memory (POH) : {currentMem / 1048576:N0} MB ({budgetUsage:F1}% of budget)");
        _ = sb.AppendLine("---------------------------------------------------------------------------");
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_METRICS(StringBuilder sb)
    {
        _ = sb.AppendLine();
        _ = sb.AppendLine("===========================================================================");
        _ = sb.AppendLine("Buffer Metrics (Shrink/Expand Operations):");
        _ = sb.AppendLine("===========================================================================");
        _ = sb.AppendLine("SIZE         | Shrink OK    | Shrink Skip  | Expand OK  | Bytes Returned   ");
        _ = sb.AppendLine("-------------+--------------+--------------+------------+------------------");

        List<SlabBucket> buckets = [.. _slabPool.GetAllBuckets()];
        buckets.Sort(static (a, b) => a.GetPoolInfo().BufferSize.CompareTo(b.GetPoolInfo().BufferSize));

        foreach (SlabBucket bucket in buckets)
        {
            BufferPoolState info = bucket.GetPoolInfo();

            if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
            {
                string bytesReturnedStr = metrics.TotalBytesReturned > 1_000_000
                    ? $"{metrics.TotalBytesReturned / 1_000_000}MB"
                    : $"{metrics.TotalBytesReturned / 1024}KB";

                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{info.BufferSize,12} | {info.Shrinks,12} | {metrics.ShrinkSkipped,12} | {info.Expands,10} | {bytesReturnedStr}");
            }
            else
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{info.BufferSize,12} | {0,12} | {0,12} | {0,10} | {"0KB"}");
            }
        }

        _ = sb.AppendLine("--------------------------------------------------------------------------");
    }

    #endregion Private: Reporting
}

