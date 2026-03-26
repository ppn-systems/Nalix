// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Internal.Buffers;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Framework.Memory.Buffers;

/// <summary>
/// Manages buffers of various sizes with optimized allocation/deallocation and optional trimming.
/// Includes safety guardrails to prevent aggressive shrinking and ensure minimum buffer availability.
/// </summary>
[DebuggerNonUserCode]
public sealed class BufferPoolManager : IDisposable, IReportable
{
    #region Fields & Constants

    private readonly BufferConfig _config;

    private readonly (int BufferSize, double Allocation)[] _bufferAllocations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _suitablePoolSizeCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, BufferPoolMetrics> _metricsCache;

    private readonly System.Buffers.ArrayPool<byte> _fallbackArrayPool = System.Buffers.ArrayPool<byte>.Shared;
    private readonly BufferPoolCollection _poolManager;
    private readonly ShrinkSafetyPolicy _shrinkPolicy;

    private int _trimCycleCount;
    private bool _isInitialized;
    private long _cachedMemoryBudget;
    private long _lastBudgetComputeTime;

    #endregion Fields & Constants

    #region Nested Types

    /// <summary>
    /// Safety policy for shrinking operations to prevent aggressive buffer reduction.
    /// </summary>
    private sealed class ShrinkSafetyPolicy
    {
        /// <summary>
        /// Minimum percentage of total buffers to retain (default 25% = safety margin).
        /// </summary>
        public double MinimumRetentionPercent { get; set; } = 0.25;

        /// <summary>
        /// Maximum buffers to shrink in a single operation (prevent sudden drops).
        /// </summary>
        public int MaxSingleShrinkStep { get; set; } = 20;

        /// <summary>
        /// Maximum percentage of total buffers to shrink per trim cycle (e.g., 20% max per 5min).
        /// </summary>
        public double MaxShrinkPercentPerCycle { get; set; } = 0.20;

        /// <summary>
        /// Minimum absolute buffers per pool (at least 1 for emergency situations).
        /// </summary>
        public int AbsoluteMinimum { get; set; } = 1;
    }

    /// <summary>
    /// Metrics for tracking shrink/expand operations on a pool.
    /// </summary>
    private struct BufferPoolMetrics
    {
        /// <summary>
        /// Total bytes returned to ArrayPool via shrinking.
        /// </summary>
        public long TotalBytesReturned;

        /// <summary>
        /// Number of successful shrink operations.
        /// </summary>
        public int ShrinkAttempted;

        /// <summary>
        /// Number of shrinks skipped due to safety checks.
        /// </summary>
        public int ShrinkSkipped;

        /// <summary>
        /// Number of successful expand operations.
        /// </summary>
        public int ExpandAttempted;

        /// <summary>
        /// Last timestamp when pool state changed.
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
    /// </summary>
    public static readonly string RecurringName;

    #endregion Properties

    #region Constructors

    static BufferPoolManager() => RecurringName = "buf.trim";

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with validation and trimming.
    /// </summary>
    public BufferPoolManager() : this(bufferConfig: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolManager"/> class with validation and trimming.
    /// </summary>
    /// <param name="bufferConfig"></param>
    public BufferPoolManager(BufferConfig? bufferConfig = null)
    {
        BufferConfig config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferConfig>();
        config.Validate();

        _config = config;

        _suitablePoolSizeCache = new();
        _metricsCache = new();
        _shrinkPolicy = new ShrinkSafetyPolicy();

        _bufferAllocations = BufferConfig.ParseBufferAllocations(config.BufferAllocations);

        MinBufferSize = Enumerable.Min(_bufferAllocations, alloc => alloc.BufferSize);
        MaxBufferSize = Enumerable.Max(_bufferAllocations, alloc => alloc.BufferSize);

        _poolManager = new BufferPoolCollection(bufferConfig: config);
        _poolManager.EventShrink += SHRINK_BUFFER_POOL_SIZE;
        _poolManager.EventIncrease += INCREASE_BUFFER_POOL_SIZE;

        ALLOCATE_BUFFERS();

        if (_config.EnableMemoryTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName, GetHashCode()),
                interval: TimeSpan.FromMinutes(Math.Max(1, _config.TrimIntervalMinutes)),
                work: _ =>
                {
                    TRIM_EXCESS_BUFFERS(null);
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

    /// <summary>
    /// Rents a buffer of at least the requested size with optimized caching and optional fallback.
    /// </summary>
    /// <param name="minimumLength"></param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Rent(int minimumLength = 256)
    {
        if (IS_FAST_COMMON_SIZE(minimumLength))
        {
            return _poolManager.RentBuffer(minimumLength);
        }

        if (_suitablePoolSizeCache.TryGetValue(minimumLength, out int cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            return RENT_FROM_POOLS_WITH_CACHING(minimumLength);
        }
        catch (ArgumentException ex)
        {
            return HANDLE_RENT_FAILURE(minimumLength, ex);
        }
    }

    /// <summary>
    /// Returns the buffer to the appropriate pool with safety checks and fallback.
    /// </summary>
    /// <param name="array"></param>
    /// <param name="arrayClear"></param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public void Return(byte[]? array, bool arrayClear = false)
    {
        if (array is null)
        {
            return;
        }

        try
        {
            RETURN_TO_MANAGED_POOLS(array);
        }
        catch (ArgumentException ex)
        {
            HANDLE_RETURN_FAILURE(array, ex);
        }
    }

    /// <summary>
    /// Rents a buffer and wraps it as an <see cref="ArraySegment{T}"/> for use with
    /// <see cref="SocketAsyncEventArgs"/> (SAEA) workflows.
    /// </summary>
    /// <param name="size">The minimum number of bytes required.</param>
    /// <returns>
    /// An <see cref="ArraySegment{T}"/> backed by a pooled buffer,
    /// with <c>Offset = 0</c> and <c>Count = size</c>.
    /// </returns>
    /// <remarks>
    /// The caller must return the underlying array via <see cref="Return(ArraySegment{byte})"/>
    /// or <see cref="ReturnFromSaea"/> after use to avoid leaking pool buffers.
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<byte> RentSegment(int size = 256)
    {
        byte[] buffer = Rent(size);
        return new ArraySegment<byte>(buffer, 0, size);
    }

    /// <summary>
    /// Returns a buffer that was rented via <see cref="RentSegment"/> back to the pool.
    /// Only the underlying <see cref="ArraySegment{T}.Array"/> is returned;
    /// <c>Offset</c> and <c>Count</c> are ignored.
    /// </summary>
    /// <param name="segment">The segment whose backing array will be returned.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(ArraySegment<byte> segment) => Return(segment.Array);

    /// <summary>
    /// Rents a buffer from the pool and assigns it to the given
    /// <see cref="SocketAsyncEventArgs"/> via
    /// <see cref="SocketAsyncEventArgs.SetBuffer(byte[], int, int)"/>.
    /// </summary>
    /// <param name="saea">The <see cref="SocketAsyncEventArgs"/> to configure.</param>
    /// <param name="size">The minimum buffer size required.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="saea"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Call <see cref="ReturnFromSaea"/> when the async operation completes to return the buffer to the pool.
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RentForSaea(
        SocketAsyncEventArgs saea,
        int size = 256)
    {
        ArgumentNullException.ThrowIfNull(saea);

        byte[] buffer = Rent(size);
        saea.SetBuffer(buffer, 0, size);
    }

    /// <summary>
    /// Returns the buffer currently assigned to a
    /// <see cref="SocketAsyncEventArgs"/> back to the pool
    /// and clears the buffer reference on the SAEA.
    /// </summary>
    /// <param name="saea">The <see cref="SocketAsyncEventArgs"/> whose buffer will be returned.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="saea"/> is <see langword="null"/>.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnFromSaea(
        SocketAsyncEventArgs saea)
    {
        ArgumentNullException.ThrowIfNull(saea);

        // Grab the buffer before clearing the reference
        byte[]? buffer = saea.Buffer;

        // Detach buffer from SAEA to avoid accidental reuse after return
        saea.SetBuffer(null, 0, 0);

        Return(buffer);
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    /// <param name="size"></param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public double GetAllocationForSize(int size)
    {
        if (size > MaxBufferSize)
        {
            return Enumerable.Last(_bufferAllocations).Allocation;
        }

        if (size <= MinBufferSize)
        {
            return Enumerable.First(_bufferAllocations).Allocation;
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
                                                : Enumerable.Last(_bufferAllocations).Allocation;
    }

    /// <summary>
    /// Generates a report on the current state of the buffer pools with metrics.
    /// </summary>
    /// <returns>A string containing the report.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new();

        APPEND_REPORT_HEADER(sb);
        APPEND_REPORT_POOL_DETAILS(sb);
        APPEND_REPORT_METRICS(sb);

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic report of the buffer pool manager and all buffer pools.
    /// </summary>
    /// <returns>A dictionary describing the state of the BufferPoolManager.</returns>
    public IDictionary<string, object> GenerateReportData()
    {
        Dictionary<string, object> data = new(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["Initialized"] = _isInitialized,
            ["TotalBuffersConfigured"] = _config.TotalBuffers,
            ["PoolCount"] = _bufferAllocations.Length,
            ["MinBufferSize"] = MinBufferSize,
            ["MaxBufferSize"] = MaxBufferSize,
            ["EnableTrimming"] = _config.EnableMemoryTrimming,
            ["EnableAnalytics"] = _config.EnableAnalytics,
            ["EnableSecureClear"] = _config.SecureClear,
            ["FallbackToArrayPool"] = _config.FallbackToArrayPool,
            ["TrimIntervalMinutes"] = _config.TrimIntervalMinutes,
            ["DeepTrimIntervalMinutes"] = _config.DeepTrimIntervalMinutes,
            ["TrimCycleCount"] = _trimCycleCount,
            ["ShrinkSafetyPolicy"] = new Dictionary<string, object>
            {
                ["MinimumRetentionPercent"] = _shrinkPolicy.MinimumRetentionPercent,
                ["MaxSingleShrinkStep"] = _shrinkPolicy.MaxSingleShrinkStep,
                ["MaxShrinkPercentPerCycle"] = _shrinkPolicy.MaxShrinkPercentPerCycle,
                ["AbsoluteMinimum"] = _shrinkPolicy.AbsoluteMinimum
            }
        };

        // Pool detail
        List<Dictionary<string, object>> poolDetails = [.. _poolManager.GetAllPools()
            .OrderBy(p => p.GetPoolInfoRef().BufferSize)
            .Select(pool =>
            {
                ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
                int inUse = info.TotalBuffers - info.FreeBuffers;
                double usage = info.GetUsageRatio() * 100.0;
                double miss = info.GetMissRate() * 100.0;
                _ = _metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics);

                string bytesReturned = metrics.TotalBytesReturned > 1_000_000
                    ? $"{metrics.TotalBytesReturned / 1_000_000}MB"
                    : $"{metrics.TotalBytesReturned / 1024}KB";

                return new Dictionary<string, object>
                {
                    ["BufferSize"] = info.BufferSize,
                    ["Total"] = info.TotalBuffers,
                    ["Free"] = info.FreeBuffers,
                    ["InUse"] = inUse,
                    ["UsageRatio"] = usage,
                    ["MissRate"] = miss,
                    ["ShrinkAttempted"] = metrics.ShrinkAttempted,
                    ["ShrinkSkipped"] = metrics.ShrinkSkipped,
                    ["ExpandAttempted"] = metrics.ExpandAttempted,
                    ["BytesReturned"] = bytesReturned
                };
            })];

        data["Pools"] = poolDetails;
        return data;
    }

    #endregion Public API

    #region Private: Rent / Return helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IS_FAST_COMMON_SIZE(int size) => size is 256 or 512 or 1024 or 2048 or 4096;

    /// <summary>
    /// Rents buffer from configured pools and optionally updates size cache.
    /// </summary>
    /// <param name="size"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte[] RENT_FROM_POOLS_WITH_CACHING(int size)
    {
        byte[] buffer = _poolManager.RentBuffer(size);

        if (_config.EnableAnalytics)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(BufferPoolManager)}:Internal] rent-fast minimumLength={size}");
        }

        CACHE_SUITABLE_POOL_SIZE(size, buffer.Length);

        return buffer;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CACHE_SUITABLE_POOL_SIZE(int requestedSize, int actualSize)
    {
        if (requestedSize is <= 64 or >= 1_000_000)
        {
            return;
        }

        if (_suitablePoolSizeCache.Count >= 1000)
        {
            return;
        }

        _ = _suitablePoolSizeCache.TryAdd(requestedSize, actualSize);
    }

    /// <summary>
    /// Handles rent failure by optionally falling back to ArrayPool.
    /// </summary>
    /// <param name="size"></param>
    /// <param name="ex"></param>
    /// <exception cref="ArgumentException"></exception>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte[] HANDLE_RENT_FAILURE(int size, ArgumentException ex)
    {
        if (_config.FallbackToArrayPool)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SH.{nameof(BufferPoolManager)}:Internal] fallback minimumLength={size} msg={ex.Message}");

            return _fallbackArrayPool.Rent(size);
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Error($"[SH.{nameof(BufferPoolManager)}:Internal] rent-fail minimumLength={size} msg={ex.Message}", ex);
        throw ex;
    }

    /// <summary>
    /// Returns a buffer to managed Nalix pools and emits analytics if enabled.
    /// </summary>
    /// <param name="buffer"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RETURN_TO_MANAGED_POOLS(byte[] buffer)
    {
        _poolManager.ReturnBuffer(buffer);

        if (_config.EnableAnalytics)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] return minimumLength={buffer.Length}");
        }
    }

    /// <summary>
    /// Handles return failure by optionally returning buffer to fallback ArrayPool.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="ex"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HANDLE_RETURN_FAILURE(byte[] buffer, ArgumentException ex)
    {
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (_config.FallbackToArrayPool)
        {
            if (_config.SecureClear)
            {
                Array.Clear(buffer, 0, buffer.Length);
            }

            _fallbackArrayPool.Return(buffer, clearArray: false);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SH.{nameof(BufferPoolManager)}:Internal] return-fallback minimumLength={buffer.Length}");

            return;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Warn($"[SH.{nameof(BufferPoolManager)}:Internal] return-fail minimumLength={buffer.Length} msg={ex.Message}");
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
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                      $"init-ok total={_config.TotalBuffers} pools={_bufferAllocations.Length} min={MinBufferSize} max={MaxBufferSize}");
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private void TRIM_EXCESS_BUFFERS(object? _)
    {
        int cycle = Interlocked.Increment(ref _trimCycleCount);
        bool deepTrim = SHOULD_RUN_DEEP_TRIM(cycle);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] trim-run deep={deepTrim}");

        // Compute memory budget once per cycle (cache it)
        (long targetBudget, long currentUsage, bool overBudget) = COMPUTE_MEMORY_BUDGET();

        foreach (BufferPoolShared pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            // Only consider TRIM logic, NOT auto-expand events (separate concern)
            if (!SHOULD_TRIM_POOL(in info, overBudget, deepTrim))
            {
                continue;
            }

            int shrinkStep = CALCULATE_SAFE_SHRINK_STEP(in info, cycle);
            if (shrinkStep <= 0)
            {
                continue;
            }

            TRIM_SINGLE_POOL(pool, in info, shrinkStep);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SHOULD_RUN_DEEP_TRIM(int cycle)
    {
        int deepEvery = Math.Max(1, _config.DeepTrimIntervalMinutes / Math.Max(1, _config.TrimIntervalMinutes));
        return (cycle % deepEvery) == 0;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private (long TargetBudget, long CurrentUsage, bool OverBudget) COMPUTE_MEMORY_BUDGET()
    {
        // Cache memory budget for 10 seconds to avoid repeated computation
        long now = Environment.TickCount64;
        const long CacheDurationMs = 10_000;

        if (now - _lastBudgetComputeTime < CacheDurationMs && _cachedMemoryBudget > 0)
        {
            // Return cached value
            long current = 0;
            foreach (BufferPoolShared pool in _poolManager.GetAllPools())
            {
                ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
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
        foreach (BufferPoolShared pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
            currentUsage += info.TotalBuffers * (long)info.BufferSize;
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
    /// <param name="info"></param>
    /// <param name="cycle"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private int CALCULATE_SAFE_SHRINK_STEP(in BufferPoolState info, int cycle)
    {
        if (info.TotalBuffers <= 0)
        {
            return 0;
        }

        // 1. Calculate target based on allocation ratio
        double targetAllocation = GetAllocationForSize(info.BufferSize);
        int targetBuffers = (int)Math.Max(
            _shrinkPolicy.AbsoluteMinimum,
            targetAllocation * _config.TotalBuffers
        );

        // 2. Enforce minimum retention percentage (default 25%)
        int minimumRetain = (int)Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = Math.Max(targetBuffers, minimumRetain);

        // 3. Calculate excess buffers
        int excessBuffers = info.FreeBuffers - targetBuffers;
        if (excessBuffers <= 0)
        {
            return 0;
        }

        // 4. Apply multiple safety limits
        int maxPerCycle = (int)Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MaxShrinkPercentPerCycle
        );

        int shrinkStep = Math.Min(excessBuffers, maxPerCycle);
        shrinkStep = Math.Min(shrinkStep, _shrinkPolicy.MaxSingleShrinkStep);

        return Math.Max(0, shrinkStep);
    }

    /// <summary>
    /// Applies trim on a single pool with metrics tracking.
    /// </summary>
    /// <param name="pool"></param>
    /// <param name="info"></param>
    /// <param name="shrinkStep"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TRIM_SINGLE_POOL(BufferPoolShared pool, in BufferPoolState info, int shrinkStep)
    {
        double usage = info.GetUsageRatio();

        pool.DecreaseCapacity(shrinkStep);

        // Track metrics
        if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.TotalBytesReturned += (long)shrinkStep * info.BufferSize;
            metrics.ShrinkAttempted++;
            metrics.LastChangeTime = Environment.TickCount64;
            _metricsCache[info.BufferSize] = metrics;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                       $"trim-shrink minimumLength={info.BufferSize} step={shrinkStep} usage={usage:F2}% " +
                                       $"remain={info.TotalBuffers - shrinkStep}");
    }

    #endregion Private: Allocation & Trimming

    #region Private: Resize Strategies

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IS_OVER_MEMORY_BUDGET()
    {
        (long _, long _, bool overBudget) = COMPUTE_MEMORY_BUDGET();
        return overBudget;
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SHRINK_BUFFER_POOL_SIZE(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        int buffersToShrink = CALCULATE_AUTO_SHRINK_AMOUNT(in poolInfo);
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

        pool.DecreaseCapacity(buffersToShrink);

        ref readonly BufferPoolState latest = ref pool.GetPoolInfoRef();

        if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics m))
        {
            m.TotalBytesReturned += (long)buffersToShrink * poolInfo.BufferSize;
            m.ShrinkAttempted++;
            m.LastChangeTime = Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = m;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] shrink minimumLength={latest.BufferSize} by={buffersToShrink}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_AUTO_SHRINK_AMOUNT(in BufferPoolState poolInfo)
    {
        if (poolInfo.TotalBuffers <= 0)
        {
            return 0;
        }

        double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        int targetBuffers = (int)(targetAllocation * _config.TotalBuffers);

        int minimumRetain = (int)Math.Ceiling(
            poolInfo.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = Math.Max(targetBuffers, minimumRetain);

        int excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        return Math.Clamp(excessBuffers, 0, _config.MaxBufferIncreaseLimit);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void INCREASE_BUFFER_POOL_SIZE(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        int threshold = Math.Max(1, (int)(poolInfo.TotalBuffers * 0.25));
        if (poolInfo.FreeBuffers > threshold)
        {
            return;
        }

        double usage = poolInfo.GetUsageRatio();
        double missRatio = poolInfo.GetMissRate();

        int increaseStep = CALCULATE_INCREASE_STEP(in poolInfo, usage, missRatio);
        if (increaseStep <= 0)
        {
            return;
        }

        if (IS_OVER_MEMORY_BUDGET())
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[SH.{nameof(BufferPoolManager)}:Internal] skip-increase minimumLength={poolInfo.BufferSize} over budget");
            return;
        }

        if (pool.FreeBuffers > threshold)
        {
            return;
        }

        pool.IncreaseCapacity(increaseStep);

        if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.ExpandAttempted++;
            metrics.LastChangeTime = Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = metrics;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                       $"increase minimumLength={poolInfo.BufferSize} by={increaseStep} usage={usage:F2}% miss={missRatio:F2}%");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_INCREASE_STEP(in BufferPoolState poolInfo, double usage, double missRatio)
    {
        int baseIncreasePow2 = Math.Max(_config.MinimumIncrease,
                (int)System.Numerics.BitOperations.RoundUpToPowerOf2(
                    (uint)Math.Max(1, poolInfo.TotalBuffers >> 2)));

        double usageFactor = 1.0 + Math.Max(0.0, (usage - 0.75) * 2.0);
        double missFactor = 1.0 + Math.Min(1.0, missRatio * 2.0);

        int scaled = (int)Math.Ceiling(
            baseIncreasePow2 * usageFactor * missFactor * _config.AdaptiveGrowthFactor);

        // Tính soft cap: tối đa 25% TotalBuffers hiện tại mỗi lần expand
        // Tránh spike lớn khi pool đang nhỏ mà miss rate đột ngột cao
        int softCap = Math.Max(
            _config.MinimumIncrease,
            (int)Math.Ceiling(poolInfo.TotalBuffers * 0.25));

        return Math.Min(scaled, Math.Min(softCap, _config.MaxBufferIncreaseLimit));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_APPLY_SHRINK(in BufferPoolState poolInfo, int buffersToShrink) => buffersToShrink > 0 && poolInfo.FreeBuffers >= buffersToShrink;

    #endregion Private: Resize Strategies

    #region Private: Reporting

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_HEADER(StringBuilder sb)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] BufferPoolManager Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Initialized: {_isInitialized}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Buffers (Configured): {_config.TotalBuffers}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Pools: {_bufferAllocations.Length}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Min Buffer SIZE: {MinBufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Max Buffer SIZE: {MaxBufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Enable Trimming: {_config.EnableMemoryTrimming}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Enable Analytics: {_config.EnableAnalytics}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Enable SecureClear: {_config.SecureClear}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Fallback to ArrayPool: {_config.FallbackToArrayPool}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Trim Interval (min): {_config.TrimIntervalMinutes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Deep Trim Interval (min): {_config.DeepTrimIntervalMinutes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Trim Cycles Run: {_trimCycleCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Shrink Safety Policy:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Minimum Retention: {_shrinkPolicy.MinimumRetentionPercent * 100:F1}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Max Single Shrink Step: {_shrinkPolicy.MaxSingleShrinkStep}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  Max Shrink Per Cycle: {_shrinkPolicy.MaxShrinkPercentPerCycle * 100:F1}%");
        _ = sb.AppendLine();
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_POOL_DETAILS(StringBuilder sb)
    {
        _ = sb.AppendLine("s_pool Details:");
        _ = sb.AppendLine("----------------------------------------------------------------------");
        _ = sb.AppendLine("SIZE     | Total  | Free   | In Use  | Usage %  | MissRate");
        _ = sb.AppendLine("----------------------------------------------------------------------");

        foreach (BufferPoolShared? pool in Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            int inUse = info.TotalBuffers - info.FreeBuffers;
            double usage = info.GetUsageRatio() * 100.0;
            double miss = info.GetMissRate() * 100.0;

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{info.BufferSize,8} | {info.TotalBuffers,6} | {info.FreeBuffers,5} | {inUse,7} | {usage,8:F2}% | {miss,7:F2}%");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_METRICS(StringBuilder sb)
    {
        _ = sb.AppendLine();
        _ = sb.AppendLine("s_pool Metrics (Shrink/Expand Operations):");
        _ = sb.AppendLine("----------------------------------------------------------------------");
        _ = sb.AppendLine("SIZE     | Shrink OK | Shrink Skip | Expand OK | Bytes Returned");
        _ = sb.AppendLine("----------------------------------------------------------------------");

        foreach (BufferPoolShared? pool in Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
            {
                string bytesReturnedStr = metrics.TotalBytesReturned > 1_000_000
                    ? $"{metrics.TotalBytesReturned / 1_000_000}MB"
                    : $"{metrics.TotalBytesReturned / 1024}KB";

                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{info.BufferSize,8} | {metrics.ShrinkAttempted,9} | {metrics.ShrinkSkipped,11} | {metrics.ExpandAttempted,9} | {bytesReturnedStr,14}");
            }
            else
            {
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{info.BufferSize,8} | {0,9} | {0,11} | {0,9} | {"0KB",14}");
            }
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
    }

    #endregion Private: Reporting

    #region IDisposable

    /// <summary>
    /// Releases all resources of the buffer pools.
    /// </summary>
    public void Dispose()
    {
        _suitablePoolSizeCache.Clear();
        _metricsCache.Clear();
        _poolManager.Dispose();

        if (_config.EnableMemoryTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelRecurring(TaskNaming.Recurring
                                    .CleanupJobId(RecurringName, GetHashCode()));
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(BufferPoolManager)}:{nameof(Dispose)}] disposed");

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
