// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Internal.Buffers;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Framework.Memory.Buffers;

/// <summary>
/// Manages pooled byte buffers, tracks pool metrics, and falls back to the shared
/// ArrayPool when a requested size cannot be satisfied by a managed pool.
/// </summary>
[DebuggerNonUserCode]
public sealed class BufferPoolManager : IDisposable, IReportable
{
    #region Fields & Constants

    private readonly ILogger? _logger;
    private readonly BufferOptions _config;

    private readonly (int BufferSize, double Allocation)[] _bufferAllocations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _suitablePoolSizeCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, BufferPoolMetrics> _metricsCache;
    private readonly System.Buffers.ArrayPool<byte> _fallbackArrayPool = System.Buffers.ArrayPool<byte>.Shared;
    private readonly ShrinkSafetyPolicy _shrinkPolicy;

    /// <summary>
    /// Slab-based buffer pool manager using standalone pinned arrays.
    /// This unified manager handles both byte[] and ArraySegment requests.
    /// </summary>
    private readonly Internal.Buffers.SlabPoolManager _slabPool;

    private int _trimCycleCount;
    private int _fallbackCount;
    private int _suitablePoolSizeCacheHits;
    private int _suitablePoolSizeCacheMisses;
    private int _disposed;
    private bool _isInitialized;
    private long _cachedMemoryBudget;
    private long _lastBudgetComputeTime;
    private long _totalBytesRented;
    private long _peakMemoryUsage;
    private readonly DateTime _startTime;

#if DEBUG
    private static readonly ConditionalWeakTable<byte[], BufferSentinel> s_activeSentinels = new();
    private static long s_totalRented;
    private static long s_totalReturned;
    private static long s_totalLeaked;

    private sealed class BufferSentinel
    {
        private readonly string _stackTrace;
        private readonly int _size;
        private bool _returned;

        public BufferSentinel(int size, bool captureStackTrace)
        {
            _size = size;
            _stackTrace = captureStackTrace ? System.Environment.StackTrace : "<stacktrace-disabled>";
            _ = Interlocked.Increment(ref s_totalRented);
        }

        public void MarkReturned()
        {
            if (!_returned)
            {
                _returned = true;
                _ = Interlocked.Increment(ref s_totalReturned);
            }
        }

        ~BufferSentinel()
        {
            if (!_returned)
            {
                _ = Interlocked.Increment(ref s_totalLeaked);
                // Log the leak
                Console.WriteLine($"\n[FW.Memory] LEAK DETECTED: Buffer of size {_size} was GC'd without being returned to the pool.");
                Console.WriteLine($"Allocation StackTrace:\n{_stackTrace}\n");
            }
        }
    }
#endif

    #endregion Fields & Constants

    #region Nested Types

    /// <summary>
    /// Safety policy for shrinking operations.
    /// The shrink path is intentionally conservative so trimming never removes too
    /// much capacity at once and causes the next burst to allocate again.
    /// </summary>
    private sealed class ShrinkSafetyPolicy
    {
        /// <summary>
        /// Minimum percentage of total buffers to retain.
        /// This floor protects the pool from shrinking itself into constant churn.
        /// </summary>
        public double MinimumRetentionPercent { get; set; } = 0.25;

        /// <summary>
        /// Maximum buffers to shrink in a single operation.
        /// This caps the amount of memory the trimmer can remove in one pass.
        /// </summary>
        public int MaxSingleShrinkStep { get; set; } = 20;

        /// <summary>
        /// Maximum percentage of total buffers to shrink per trim cycle.
        /// This prevents a single trim job from collapsing the pool too aggressively.
        /// </summary>
        public double MaxShrinkPercentPerCycle { get; set; } = 0.20;

        /// <summary>
        /// Minimum absolute buffers per pool.
        /// The pool always keeps at least one buffer alive so it can recover quickly.
        /// </summary>
        public int AbsoluteMinimum { get; set; } = 1;
    }

    /// <summary>
    /// Metrics for tracking shrink/expand operations on a pool.
    /// These counters are used for diagnostics and for validating trim safety
    /// decisions over time.
    /// </summary>
    private struct BufferPoolMetrics
    {
        /// <summary>
        /// Total bytes returned to ArrayPool via shrinking.
        /// This is the amount of memory the pool actually gave back.
        /// </summary>
        public long TotalBytesReturned;

        /// <summary>
        /// Number of successful shrink operations.
        /// Useful for seeing whether trimming is actively doing work or mostly idle.
        /// </summary>
        public int ShrinkAttempted;

        /// <summary>
        /// Number of shrinks skipped due to safety checks.
        /// High values here usually mean the pool is already at or near its floor.
        /// </summary>
        public int ShrinkSkipped;

        /// <summary>
        /// Number of successful expand operations.
        /// This tells us how often the pool had to grow to satisfy demand.
        /// </summary>
        public int ExpandAttempted;

        /// <summary>
        /// Last timestamp when pool state changed.
        /// Helps correlate trimming decisions with recent allocation pressure.
        /// </summary>
        public long LastChangeTime;
    }

    #endregion Nested Types

    #region Properties

    /// <summary>Gets the largest buffer size from the buffer allocations list.</summary>
    public int MaxBufferSize { get; }

    /// <summary>Gets the smallest buffer size from the buffer allocations list.</summary>
    public int MinBufferSize { get; }

    /// <summary>
    /// Gets the recurring name used for buffer trimming operations.
    /// This value is embedded in the recurring job name so trimming jobs from
    /// different manager instances remain distinct.
    /// </summary>
    public static readonly string RecurringName;

    #endregion Properties

    #region Constructors

    static BufferPoolManager() => RecurringName = "buf.trim";

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class.
    /// </summary>
    public BufferPoolManager() : this(bufferConfig: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for emitting internal events and diagnostics.</param>
    public BufferPoolManager(ILogger logger) : this(bufferConfig: null, logger) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class.
    /// </summary>
    /// <param name="bufferConfig">
    /// The buffer configuration to use. If <see langword="null"/>, the default configuration is loaded.
    /// </param>
    /// <param name="logger">
    /// Optional logger for emitting internal events and diagnostics.
    /// </param>
    public BufferPoolManager(BufferOptions? bufferConfig = null, ILogger? logger = null)
    {
        BufferOptions config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferOptions>();
        config.Validate();

        _logger = logger;
        _config = config;

        _suitablePoolSizeCache = new();
        _metricsCache = new();
        _shrinkPolicy = new ShrinkSafetyPolicy();
        _slabPool = new Internal.Buffers.SlabPoolManager();
        _slabPool.ResizeOccurred += this.HANDLE_BUFFER_POOL_RESIZE;
        _startTime = DateTime.UtcNow;

        _bufferAllocations = BufferOptions.ParseBufferAllocations(config.BufferAllocations);
        this.MinBufferSize = _bufferAllocations.Length > 0 ? _bufferAllocations[0].BufferSize : 0;
        this.MaxBufferSize = _bufferAllocations.Length > 0 ? _bufferAllocations[^1].BufferSize : 0;

        this.ALLOCATE_BUFFERS();

        if (_config.EnableMemoryTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()),
                interval: TimeSpan.FromMinutes(Math.Max(1, _config.TrimIntervalMinutes)),
                work: _ =>
                {
                    this.TRIM_EXCESS_BUFFERS(null);
                    return ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    Jitter = TimeSpan.FromSeconds(5),
                    ExecutionTimeout = TimeSpan.FromSeconds(5),
                    BackoffCap = TimeSpan.FromMinutes(1)
                }
            );
        }
    }

    #endregion Constructors

    #region Public API

    /// <summary>Rents a buffer of at least the requested size.</summary>
    /// <param name="minimumLength">The minimum number of bytes required.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="minimumLength"/> cannot be serviced by the configured pools and fallback is disabled.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Rent(int minimumLength = 256)
    {

        /*
         * [Fast Path 1: Direct Bucket Match]
         * We first try to rent directly from a slab bucket that exactly matches 
         * or is suitable for the requested size.
         */
        if (_slabPool.TryRent(minimumLength, out byte[]? array))
        {
            goto ReturnArray;
        }

        /*
         * [Fast Path 2: Cached Suitable Size]
         * If a direct match fails, we check our cache to see if we've already 
         * calculated a suitable larger bucket for this specific requested size.
         */
        if (_suitablePoolSizeCache.TryGetValue(minimumLength, out int cachedPoolSize))
        {
            if (_slabPool.TryRent(cachedPoolSize, out array))
            {
                _ = Interlocked.Increment(ref _suitablePoolSizeCacheHits);
                goto ReturnArray;
            }
        }

        _ = Interlocked.Increment(ref _suitablePoolSizeCacheMisses);

        try
        {
            if (this.TRY_RENT_ARRAY_WITH_CACHING(minimumLength, out array))
            {
                goto ReturnArray;
            }

            // Should not happen if bucket exists, but fallback if TryRentArray returned false.
            throw new ArgumentException("No suitable bucket found.");
        }
        catch (ArgumentException ex)
        {
            return this.HANDLE_RENT_FAILURE(minimumLength, ex);
        }

    ReturnArray:
        _ = Interlocked.Add(ref _totalBytesRented, array.Length);
#if DEBUG
        _ = s_activeSentinels.GetValue(array, arr => new BufferSentinel(arr.Length, _config.EnableBufferLeakStackTrace));
#endif
        return array;
    }

    /// <summary>Returns a buffer to the appropriate pool.</summary>
    /// <param name="array">The buffer to return.</param>
    /// <param name="arrayClear">Whether the buffer should be cleared before returning it.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(byte[]? array, bool arrayClear = false)
    {
        if (array is null)
        {
            return;
        }

        if (arrayClear)
        {
            array.AsSpan().Clear();
        }

#if DEBUG
        if (s_activeSentinels.TryGetValue(array, out BufferSentinel? sentinel))
        {
            sentinel.MarkReturned();
            s_activeSentinels.Remove(array);
        }
#endif

        if (!_slabPool.TryReturn(array))
        {
            this.HANDLE_RETURN_FAILURE(array, new ArgumentException($"Buffer of size {array.Length} not owned by managed pools."));
        }
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    /// <param name="size">The buffer size being queried.</param>
    /// <exception cref="InvalidOperationException">Thrown when buffer allocation metadata is unavailable.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public double GetAllocationForSize(int size)
    {
        if (size > this.MaxBufferSize)
        {
            return _bufferAllocations[^1].Allocation;
        }

        if (size <= this.MinBufferSize)
        {
            return _bufferAllocations[0].Allocation;
        }

        int left = 0;
        int right = _bufferAllocations.Length - 1;

        while (left <= right)
        {
            int mid = left + ((right - left) / 2);
            int midSize = _bufferAllocations[mid].BufferSize;

            if (midSize == size)
            {
                return _bufferAllocations[mid].Allocation;
            }

            if (midSize < size)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return left < _bufferAllocations.Length ? _bufferAllocations[left].Allocation
                                                : _bufferAllocations[^1].Allocation;
    }

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

#if DEBUG
        sb.AppendLine("Lease Tracking (DEBUG):");
        sb.AppendLine("-----------------------------------------------------------------------------");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Rented         : {Volatile.Read(ref s_totalRented)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Returned       : {Volatile.Read(ref s_totalReturned)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Active         : {Volatile.Read(ref s_totalRented) - Volatile.Read(ref s_totalReturned)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Leaked         : {Volatile.Read(ref s_totalLeaked)}");
        sb.AppendLine("-----------------------------------------------------------------------------");
        sb.AppendLine();
#endif

        this.APPEND_REPORT_POOL_DETAILS(sb);
        this.APPEND_REPORT_METRICS(sb);

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic report of the buffer pool manager and all buffer pools.
    /// This shape is easier for tooling and log pipelines to consume than the
    /// formatted text report.
    /// </summary>
    /// <returns>A dictionary describing the state of the BufferPoolManager.</returns>
    public IDictionary<string, object> GetReportData()
    {
        Dictionary<string, object> data = new(16, StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["Initialized"] = _isInitialized,
            ["TotalBuffersConfigured"] = _config.TotalBuffers,
            ["PoolCount"] = _bufferAllocations.Length,
            ["MinBufferSize"] = this.MinBufferSize,
            ["MaxBufferSize"] = this.MaxBufferSize,
            ["EnableTrimming"] = _config.EnableMemoryTrimming,
            ["EnableAnalytics"] = _config.EnableAnalytics,
            ["FallbackToArrayPool"] = _config.FallbackToArrayPool,
            ["TrimIntervalMinutes"] = _config.TrimIntervalMinutes,
            ["DeepTrimIntervalMinutes"] = _config.DeepTrimIntervalMinutes,
            ["TrimCycleCount"] = _trimCycleCount,
            ["FallbackCount"] = _fallbackCount,
            ["BucketCacheHits"] = _suitablePoolSizeCacheHits,
            ["BucketCacheMisses"] = _suitablePoolSizeCacheMisses,
            ["PeakMemoryUsageBytes"] = _peakMemoryUsage,
            ["ThroughputMBps"] = (DateTime.UtcNow - _startTime).TotalSeconds > 0
                ? (double)Volatile.Read(ref _totalBytesRented) / (1024 * 1024) / (DateTime.UtcNow - _startTime).TotalSeconds
                : 0,
            ["ShrinkSafetyPolicy"] = new Dictionary<string, object>(4, StringComparer.Ordinal)
            {
                ["MinimumRetentionPercent"] = _shrinkPolicy.MinimumRetentionPercent,
                ["MaxSingleShrinkStep"] = _shrinkPolicy.MaxSingleShrinkStep,
                ["MaxShrinkPercentPerCycle"] = _shrinkPolicy.MaxShrinkPercentPerCycle,
                ["AbsoluteMinimum"] = _shrinkPolicy.AbsoluteMinimum
            }
        };

#if DEBUG
        data["LeaseTracking"] = new Dictionary<string, object>(4, StringComparer.Ordinal)
        {
            ["TotalRented"] = Volatile.Read(ref s_totalRented),
            ["TotalReturned"] = Volatile.Read(ref s_totalReturned),
            ["TotalActive"] = Volatile.Read(ref s_totalRented) - Volatile.Read(ref s_totalReturned),
            ["TotalLeaked"] = Volatile.Read(ref s_totalLeaked)
        };
#endif

        IReadOnlyCollection<SlabBucket> allBuckets = _slabPool.GetAllBuckets();
        List<Dictionary<string, object>> poolDetails = new(allBuckets.Count);

        long totalHits = 0;
        long totalMisses = 0;
        long totalExpands = 0;
        long totalShrinks = 0;

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

            poolDetails.Add(new Dictionary<string, object>(13, StringComparer.Ordinal)
            {
                ["BufferSize"] = info.BufferSize,
                ["Initial"] = info.InitialCapacity,
                ["Total"] = info.TotalBuffers,
                ["Free"] = info.FreeBuffers,
                ["InUse"] = inUse,
                ["Hits"] = info.Hits,
                ["Expands"] = info.Expands,
                ["Shrinks"] = info.Shrinks,
                ["UsageRatio"] = usage,
                ["MissRate"] = miss,
                ["ShrinkSkipped"] = metrics.ShrinkSkipped,
                ["BytesReturned"] = bytesReturned
            });
        }

        data["Pools"] = poolDetails;
        data["TotalHits"] = totalHits;
        data["TotalMisses"] = totalMisses;
        data["TotalExpands"] = totalExpands;
        data["TotalShrinks"] = totalShrinks;
        data["HitRate"] = (totalHits + totalMisses) > 0 ? (double)totalHits / (totalHits + totalMisses) : 1.0;

        return data;
    }

    #endregion Public API
    #region Private: Rent / Return helpers

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TRY_RENT_ARRAY_WITH_CACHING(int size, [NotNullWhen(true)] out byte[]? array)
    {
        if (_slabPool.TryRent(size, out array))
        {
            this.CACHE_SUITABLE_POOL_SIZE(size, array.Length);
            return true;
        }

        return false;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CACHE_SUITABLE_POOL_SIZE(int requestedSize, int actualSize)
    {
        // Tiny requests and huge one-off requests are poor cache candidates and
        // would mostly add noise to the size-to-pool mapping.
        if (requestedSize is <= 64 or >= 1_000_000)
        {
            return;
        }

        if (_suitablePoolSizeCache.Count >= _config.SuitablePoolSizeCacheLimit)
        {
            return;
        }

        _ = _suitablePoolSizeCache.TryAdd(requestedSize, actualSize);
    }

    /// <summary>
    /// Handles rent failure by optionally falling back to ArrayPool.
    /// This is the safety net for requests that the configured pools cannot
    /// satisfy or that are rejected by the pool layout.
    /// </summary>
    /// <param name="size">The requested buffer size.</param>
    /// <param name="ex">The exception describing the rent failure.</param>
    /// <exception cref="ArgumentException">Rethrown when the requested buffer size is invalid and fallback is disabled.</exception>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte[] HANDLE_RENT_FAILURE(int size, ArgumentException ex)
    {
        if (_config.FallbackToArrayPool)
        {
            // If fallback is enabled, return a shared ArrayPool buffer instead of
            // failing the operation outright.
            _logger?.Warn($"[SH.{nameof(BufferPoolManager)}:Internal] fallback minimumLength={size} msg={ex.Message}");
            _ = Interlocked.Increment(ref _fallbackCount);

            return _fallbackArrayPool.Rent(size);
        }

        _logger?.Error($"[SH.{nameof(BufferPoolManager)}:Internal] rent-fail minimumLength={size} msg={ex.Message}", ex);
        ExceptionDispatchInfo.Capture(ex).Throw();
        throw new InvalidOperationException("Unreachable");
    }


    /// <summary>
    /// Handles return failure by optionally returning buffer to fallback ArrayPool.
    /// This is the mirror path for fallback allocations and buffers that do not
    /// match a managed Nalix pool exactly.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="ex">The exception describing the return failure.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HANDLE_RETURN_FAILURE(byte[] buffer, ArgumentException ex)
    {
        if (_config.FallbackToArrayPool)
        {
            _fallbackArrayPool.Return(buffer, clearArray: false);
            _logger?.Debug($"[SH.{nameof(BufferPoolManager)}:Internal] return-fallback minimumLength={buffer.Length}");

            return;
        }

        _logger?.Warn($"[SH.{nameof(BufferPoolManager)}:Internal] return-fail minimumLength={buffer.Length} msg={ex.Message}");
    }

    #endregion Private: Rent / Return helpers

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
        _logger?.Info($"[SH.{nameof(BufferPoolManager)}:Internal] init-ok total={_config.TotalBuffers} buckets={_bufferAllocations.Length}");
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void TRIM_EXCESS_BUFFERS(object? _)
    {
        /*
         * [Memory Trimming Lifecycle]
         * 1. Increment cycle count and determine if this is a 'deep trim' cycle.
         * 2. Compute the current memory budget based on GC state and hard limits.
         * 3. Iterate through all buckets and apply conservative shrinking.
         */
        int cycle = Interlocked.Increment(ref _trimCycleCount);
        bool deepTrim = this.SHOULD_RUN_DEEP_TRIM(cycle);

        _logger?.Trace($"[SH.{nameof(BufferPoolManager)}:Internal] trim-run deep={deepTrim}");

        // Compute memory budget once per cycle (cache it)
        (long targetBudget, long currentUsage, bool overBudget) = this.COMPUTE_MEMORY_BUDGET();

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

        if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.TotalBytesReturned += (long)shrinkStep * info.BufferSize;
            metrics.ShrinkAttempted++;
            metrics.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[info.BufferSize] = metrics;
        }

        _logger?.Trace($"[SH.{nameof(BufferPoolManager)}:Internal] trim-shrink minimumLength={info.BufferSize} step={shrinkStep} usage={usage:F2}%");
    }

    #endregion Private: Allocation & Trimming

    #region Private: Resize Strategies

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IS_OVER_MEMORY_BUDGET()
    {
        (long _, long _, bool overBudget) = this.COMPUTE_MEMORY_BUDGET();
        return overBudget;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SHRINK_BUCKET_SIZE(SlabBucket bucket)
    {
        BufferPoolState poolInfo = bucket.GetPoolInfo();

        int buffersToShrink = this.CALCULATE_AUTO_SHRINK_AMOUNT(in poolInfo);
        if (buffersToShrink <= 0)
        {
            return;
        }

        if (!SHOULD_APPLY_SHRINK(in poolInfo, buffersToShrink))
        {
            if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics metrics))
            {
                metrics.ShrinkSkipped++;
                _metricsCache[poolInfo.BufferSize] = metrics;
            }
            return;
        }

        bucket.DecreaseCapacity(buffersToShrink);

        if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics m))
        {
            m.TotalBytesReturned += (long)buffersToShrink * poolInfo.BufferSize;
            m.ShrinkAttempted++;
            m.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = m;
        }

        _logger?.Trace($"[SH.{nameof(BufferPoolManager)}:Internal] shrink minimumLength={poolInfo.BufferSize} by={buffersToShrink}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_AUTO_SHRINK_AMOUNT(in BufferPoolState poolInfo)
    {
        if (poolInfo.TotalBuffers <= 0)
        {
            return 0;
        }

        double targetAllocation = this.GetAllocationForSize(poolInfo.BufferSize);
        int targetBuffers = (int)(targetAllocation * _config.TotalBuffers);

        int minimumRetain = (int)Math.Ceiling(
            poolInfo.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = Math.Max(targetBuffers, Math.Max(minimumRetain, poolInfo.InitialCapacity));

        int excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        return Math.Clamp(excessBuffers, 0, _config.MaxBufferIncreaseLimit);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void INCREASE_BUCKET_SIZE(SlabBucket bucket)
    {
        BufferPoolState poolInfo = bucket.GetPoolInfo();

        int threshold = Math.Max(1, (int)(poolInfo.TotalBuffers * _config.ExpansionSoftCapRatio));
        if (poolInfo.FreeBuffers > threshold)
        {
            return;
        }

        double usage = poolInfo.GetUsageRatio();
        double missRatio = poolInfo.GetMissRate();

        int increaseStep = this.CALCULATE_INCREASE_STEP(in poolInfo, usage, missRatio);
        if (increaseStep <= 0)
        {
            return;
        }

        if (this.IS_OVER_MEMORY_BUDGET())
        {
            _logger?.Warn($"[SH.{nameof(BufferPoolManager)}:Internal] skip-increase minimumLength={poolInfo.BufferSize} over budget");
            return;
        }

        bucket.IncreaseCapacity(increaseStep);

        if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.ExpandAttempted++;
            metrics.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = metrics;
        }

        _logger?.Trace($"[SH.{nameof(BufferPoolManager)}:Internal] increase minimumLength={poolInfo.BufferSize} by={increaseStep}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_INCREASE_STEP(in BufferPoolState poolInfo, double usage, double missRatio)
    {
        int baseIncreasePow2 = Math.Max(_config.MinimumIncrease,
                (int)System.Numerics.BitOperations.RoundUpToPowerOf2(
                    (uint)Math.Max(1, (int)(poolInfo.TotalBuffers * _config.ExpansionSoftCapRatio))));

        double usageFactor = 1.0 + Math.Max(0.0, (usage - _config.UsageAggressiveFactor) * 2.0);
        double missFactor = 1.0 + Math.Min(1.0, missRatio * _config.MissRateAggressiveFactor);

        int scaled = (int)Math.Ceiling(
            baseIncreasePow2 * usageFactor * missFactor * _config.AdaptiveGrowthFactor);

        // Tính soft cap: tối đa X% TotalBuffers hiện tại mỗi lần expand
        // Tránh spike lớn khi pool đang nhỏ mà miss rate đột ngột cao
        int softCap = Math.Max(
            _config.MinimumIncrease,
            (int)Math.Ceiling(poolInfo.TotalBuffers * _config.ExpansionSoftCapRatio));

        return Math.Min(scaled, Math.Min(softCap, _config.MaxBufferIncreaseLimit));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_APPLY_SHRINK(in BufferPoolState poolInfo, int buffersToShrink) => buffersToShrink > 0 && poolInfo.FreeBuffers >= buffersToShrink;

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HANDLE_BUFFER_POOL_RESIZE(SlabBucket bucket, BufferPoolResizeDirection direction)
    {
        if (direction == BufferPoolResizeDirection.Increase)
        {
            this.INCREASE_BUCKET_SIZE(bucket);
            return;
        }

        this.SHRINK_BUCKET_SIZE(bucket);
    }

    #endregion Private: Resize Strategies

    #region Private: Reporting

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
        double throughputMBps = uptimeSec > 0 ? (double)Volatile.Read(ref _totalBytesRented) / (1024 * 1024) / uptimeSec : 0;
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

    #region IDisposable

    /// <summary>
    /// Releases all resources of the buffer pools.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _suitablePoolSizeCache.Clear();
        _metricsCache.Clear();
        _slabPool.Dispose();

        if (_config.EnableMemoryTrimming)
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelRecurring(TaskNaming.Recurring
                                    .CleanupJobId(RecurringName, this.GetHashCode()));
        }

        _logger?.Info($"[SH.{nameof(BufferPoolManager)}:{nameof(Dispose)}] disposed");

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
