// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Framework.Memory.Internal.Buffers;

namespace Nalix.Framework.Memory.Buffers;

public sealed partial class BufferPoolManager
{
    #region Private: Allocation & Trimming

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ALLOCATE_BUFFERS()
    {
        if (_isInitialized)
        {
            return;
        }

        foreach ((int bufferSize, double allocation) in _bufferAllocations)
        {
            int capacity = Math.Max(1, (int)(_config.TotalBuffers * allocation));
            _slabPool.CreateBucket(bufferSize, capacity);
        }

        _isInitialized = true;
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolExpanded))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolExpanded, new { _config.TotalBuffers, BucketCount = _bufferAllocations.Length, Phase = "Init" });
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void TRIM_EXCESS_BUFFERS()
    {
        /*
         * [Memory Trimming Lifecycle]
         * 1. Increment cycle count and determine if this is a 'deep trim' cycle.
         * 2. Compute the current memory budget based on GC state and hard limits.
         * 3. Iterate through all buckets and apply conservative shrinking.
         */
        int cycle = Interlocked.Increment(ref _trimCycleCount);
        bool deepTrim = this.SHOULD_RUN_DEEP_TRIM(cycle);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolTrimmed, new
            {
                DeepTrim = deepTrim,
                Phase = "BufferTrimRun"
            });
        }

        // Compute memory budget once per cycle (cache it)
        (long _, long _, bool overBudget) = this.COMPUTE_MEMORY_BUDGET();

        foreach (SlabBucket bucket in _slabPool.GetAllBuckets())
        {
            BufferPoolState info = bucket.GetPoolInfo();

            if (!SHOULD_TRIM_POOL(in info, overBudget, deepTrim))
            {
                continue;
            }

            int shrinkStep = this.CALCULATE_SAFE_SHRINK_STEP(in info, cycle);
            if (shrinkStep <= 0)
            {
                continue;
            }

            this.TRIM_SINGLE_BUCKET(bucket, in info, shrinkStep);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SHOULD_RUN_DEEP_TRIM(int cycle)
    {
        int deepEvery = Math.Max(1, _config.DeepTrimIntervalMinutes / Math.Max(1, _config.TrimIntervalMinutes));
        // Deep trimming is intentionally less frequent so routine trim cycles stay conservative.
        return (cycle % deepEvery) == 0;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private (long TargetBudget, long CurrentUsage, bool OverBudget) COMPUTE_MEMORY_BUDGET()
    {
        /*
         * [Memory Budget Calculation]
         * We calculate the budget by taking the MIN of:
         * a) (Total System Memory * Configured Percentage)
         * b) Configured Hard Cap (MaxMemoryBytes)
         *
         * This allows the pool to be "environment-aware" and shrink when 
         * system memory pressure is high.
         */
        long now = System.Environment.TickCount64;
        const long CacheDurationMs = 10_000;

        if (now - _lastBudgetComputeTime < CacheDurationMs && _cachedMemoryBudget > 0)
        {
            long current = 0;
            foreach (SlabBucket bucket in _slabPool.GetAllBuckets())
            {
                BufferPoolState info = bucket.GetPoolInfo();
                current += info.TotalBuffers * (long)info.BufferSize;
            }

            return (_cachedMemoryBudget, current, current > _cachedMemoryBudget);
        }

        long totalAvailable = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        long percentBudget = (long)(totalAvailable * _config.MaxMemoryPercentage);
        long hardCap = _config.MaxMemoryBytes > 0 ? _config.MaxMemoryBytes : long.MaxValue;

        long targetBudget = Math.Min(percentBudget, hardCap);

        _lastBudgetComputeTime = now;
        _cachedMemoryBudget = targetBudget;

        long currentUsage = 0;
        foreach (SlabBucket bucket in _slabPool.GetAllBuckets())
        {
            BufferPoolState info = bucket.GetPoolInfo();
            currentUsage += info.TotalBuffers * (long)info.BufferSize;
        }

        long peak = Volatile.Read(ref _peakMemoryUsage);
        while (currentUsage > peak)
        {
            _ = Interlocked.CompareExchange(ref _peakMemoryUsage, currentUsage, peak);
            peak = Volatile.Read(ref _peakMemoryUsage);
        }

        return (targetBudget, currentUsage, currentUsage > targetBudget);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_TRIM_POOL(in BufferPoolState info, bool overBudget, bool deepTrim)
    {
        // Skip if very low idle time
        double usage = info.GetUsageRatio();
        if (usage > 0.95 && !overBudget && !deepTrim)
        {
            return false;
        }

        bool candidateByFree = info.FreeBuffers >= (int)(info.TotalBuffers * 0.50);
        bool candidateByOverBudget = overBudget || deepTrim;

        return candidateByFree || candidateByOverBudget;
    }

    /// <summary>
    /// Calculates shrink step with safety guardrails to prevent aggressive reduction.
    /// </summary>
    /// <param name="info">The current pool state snapshot.</param>
    /// <param name="cycle">The current trim cycle number.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private int CALCULATE_SAFE_SHRINK_STEP(in BufferPoolState info, int cycle)
    {
        /*
         * [Safe Shrink Step Calculation]
         * We apply 4 layers of safety before shrinking a pool:
         * 1. Target Size: Based on the configured allocation ratio.
         * 2. Retention Floor: Never shrink below initial capacity or a % of current size.
         * 3. Liveness: Only trim buffers that are currently free.
         * 4. Damping: Cap the shrink amount per cycle to avoid oscillations.
         */
        if (info.TotalBuffers <= 0)
        {
            return 0;
        }

        // 1. Translate the configured allocation ratio into a target pool size.
        double targetAllocation = this.GetAllocationForSize(info.BufferSize);
        int targetBuffers = (int)Math.Max(
            _shrinkPolicy.AbsoluteMinimum,
            targetAllocation * _config.TotalBuffers
        );

        // 2. Never shrink below the retention floor OR the initial capacity, even if the allocation ratio is lower.
        int minimumRetain = (int)Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = Math.Max(targetBuffers, Math.Max(minimumRetain, info.InitialCapacity));

        // 3. Only trim from buffers that are actually free.
        int excessBuffers = info.FreeBuffers - targetBuffers;
        if (excessBuffers <= 0)
        {
            return 0;
        }

        // 4. Cap the trim step per cycle so the pool does not oscillate on short idle bursts.
        int maxPerCycle = (int)Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MaxShrinkPercentPerCycle
        );

        int shrinkStep = Math.Min(excessBuffers, maxPerCycle);
        shrinkStep = Math.Min(shrinkStep, _shrinkPolicy.MaxSingleShrinkStep);

        return Math.Max(0, shrinkStep);
    }

    /// <summary>
    /// Applies trim on a single bucket with metrics tracking.
    /// </summary>
    /// <param name="bucket">The bucket to trim.</param>
    /// <param name="info">The current pool state snapshot.</param>
    /// <param name="shrinkStep">The number of buffers to remove.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TRIM_SINGLE_BUCKET(SlabBucket bucket, in BufferPoolState info, int shrinkStep)
    {
        double usage = info.GetUsageRatio();

        bucket.DecreaseCapacity(shrinkStep);

        BufferPoolMetrics metrics = _metricsCache.GetOrAdd(info.BufferSize, _ => default);
        metrics.TotalBytesReturned += (long)shrinkStep * info.BufferSize;
        metrics.ShrinkAttempted++;
        metrics.LastChangeTime = System.Environment.TickCount64;
        _metricsCache[info.BufferSize] = metrics;

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolTrimmed, new
            {
                Usage = usage,
                info.BufferSize,
                Step = shrinkStep
            });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetTotalBytesRented()
    {
        long total = 0;
        foreach (SlabBucket bucket in _slabPool.GetAllBuckets())
        {
            total += bucket.GetTotalBytesRented();
        }
        return total;
    }

    #endregion Private: Allocation & Trimming
}

