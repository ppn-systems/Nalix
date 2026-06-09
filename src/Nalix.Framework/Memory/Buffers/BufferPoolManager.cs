// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Injection;
using Nalix.Environment.Configuration;
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
[Injectable(typeof(IBufferPoolManager))]
public sealed partial class BufferPoolManager : IBufferPoolManager, IDisposable
{
    #region Fields & Constants

    private readonly BufferOptions _config;

    private readonly ConcurrentDictionary<int, int> _suitablePoolSizeCache;
    private readonly ConcurrentDictionary<int, BufferPoolMetrics> _metricsCache;

    private readonly ShrinkSafetyPolicy _shrinkPolicy;
    private readonly (int BufferSize, double Allocation)[] _bufferAllocations;
    private readonly ArrayPool<byte> _fallbackArrayPool = ArrayPool<byte>.Shared;

    private ConcurrentBag<WeakReference<BufferSentinel>> _sentinelTracker = new();
    private readonly ConditionalWeakTable<byte[], BufferSentinel> _activeSentinels = new();

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
    private long _peakMemoryUsage;

    private readonly DateTime _startTime;
    private readonly IRecurringHandle? _trimJob;

    #endregion Fields & Constants

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
    /// <param name="bufferConfig">
    /// The buffer configuration to use. If <see langword="null"/>, the default configuration is loaded.
    /// </param>
    public BufferPoolManager(BufferOptions? bufferConfig = null)
    {
        BufferOptions config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferOptions>();
        config.Validate();

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
            _trimJob = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()),
                interval: TimeSpan.FromMinutes(Math.Max(1, _config.TrimIntervalMinutes)),
                work: _ =>
                {
                    this.TRIM_EXCESS_BUFFERS();
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

        array = this.RENT_SAFE(minimumLength);

    ReturnArray:
        if (_config.EnableBufferLeakDetection)
        {
            BufferSentinel sentinel = new(array, _config.EnableBufferLeakStackTrace);

            _activeSentinels.Add(array, sentinel);
            _sentinelTracker.Add(new WeakReference<BufferSentinel>(sentinel));
        }

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

        if (_config.EnableBufferLeakDetection)
        {
            if (_activeSentinels.TryGetValue(array, out BufferSentinel? sentinel))
            {
                sentinel.MarkReturned();
                _ = _activeSentinels.Remove(array);
            }
        }

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

    #endregion Public API

    #region Private: Rent / Return helpers

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte[] RENT_SAFE(int minimumLength)
    {
        try
        {
            if (this.TRY_RENT_ARRAY_WITH_CACHING(minimumLength, out byte[]? array))
            {
                return array;
            }

            throw new ArgumentException("No suitable bucket found.");
        }
        catch (ArgumentException ex)
        {
            return this.HANDLE_RENT_FAILURE(minimumLength, ex);
        }
    }

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
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Memory.PoolFailure, new DiagnosticLog("FW.BufferPoolManager:Internal", $"pool-failure size={size} error={ex.Message} fallback=true"));
            }
            _ = Interlocked.Increment(ref _fallbackCount);

            return _fallbackArrayPool.Rent(size);
        }

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Memory.PoolFailure, new DiagnosticLog("FW.BufferPoolManager:Internal", $"pool-failure size={size} error={ex.Message} fallback=false"));
        }
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
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.BufferReleased))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Memory.BufferReleased, new DiagnosticLog("FW.BufferPoolManager:Internal", $"buffer-released size={buffer.Length} fallback=true"));
            }

            return;
        }

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Memory.PoolFailure, new DiagnosticLog("FW.BufferPoolManager:Internal", $"pool-failure size={buffer.Length} error={ex.Message} fallback=false"));
        }
    }

    #endregion Private: Rent / Return helpers

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

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Memory.PoolTrimmed, new DiagnosticLog("FW.BufferPoolManager:Internal", $"pool-trimmed size={poolInfo.BufferSize} shrunk-by={buffersToShrink} phase=Shrink"));
        }
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
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Memory.PoolFailure, new DiagnosticLog("FW.BufferPoolManager:AllocateBuffers", $"pool-failure size={poolInfo.BufferSize} error=over-budget"));
            }
            return;
        }

        bucket.IncreaseCapacity(increaseStep);

        if (_metricsCache.TryGetValue(poolInfo.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.ExpandAttempted++;
            metrics.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = metrics;
        }

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolExpanded))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Memory.PoolExpanded, new DiagnosticLog("FW.BufferPoolManager:AllocateBuffers", $"pool-expanded size={poolInfo.BufferSize} increased-by={increaseStep}"));
        }
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

        _trimJob?.Dispose();

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Memory.PoolTrimmed, new DiagnosticLog("FW.BufferPoolManager:Internal", "pool-trimmed phase=dispose"));
        }

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
