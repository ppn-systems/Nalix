// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Shared.Memory.Internal.Buffers;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages buffers of various sizes with optimized allocation/deallocation and optional trimming.
/// Includes safety guardrails to prevent aggressive shrinking and ensure minimum buffer availability.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public sealed class BufferPoolManager : System.IDisposable, IReportable
{
    #region Fields & Constants

    private readonly BufferConfig _config;

    private readonly (System.Int32 BufferSize, System.Double Allocation)[] _bufferAllocations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, System.Int32> _suitablePoolSizeCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, BufferPoolMetrics> _metricsCache;

    private readonly System.Buffers.ArrayPool<System.Byte> _fallbackArrayPool = System.Buffers.ArrayPool<System.Byte>.Shared;
    private readonly BufferPoolCollection _poolManager;
    private readonly ShrinkSafetyPolicy _shrinkPolicy;

    private System.Int32 _trimCycleCount;
    private System.Boolean _isInitialized;
    private System.Int64 _cachedMemoryBudget;
    private System.Int64 _lastBudgetComputeTime;

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
        public System.Double MinimumRetentionPercent { get; set; } = 0.25;

        /// <summary>
        /// Maximum buffers to shrink in a single operation (prevent sudden drops).
        /// </summary>
        public System.Int32 MaxSingleShrinkStep { get; set; } = 20;

        /// <summary>
        /// Maximum percentage of total buffers to shrink per trim cycle (e.g., 20% max per 5min).
        /// </summary>
        public System.Double MaxShrinkPercentPerCycle { get; set; } = 0.20;

        /// <summary>
        /// Minimum absolute buffers per pool (at least 1 for emergency situations).
        /// </summary>
        public System.Int32 AbsoluteMinimum { get; set; } = 1;
    }

    /// <summary>
    /// Metrics for tracking shrink/expand operations on a pool.
    /// </summary>
    private struct BufferPoolMetrics
    {
        /// <summary>
        /// Total bytes returned to ArrayPool via shrinking.
        /// </summary>
        public System.Int64 TotalBytesReturned;

        /// <summary>
        /// Number of successful shrink operations.
        /// </summary>
        public System.Int32 ShrinkAttempted;

        /// <summary>
        /// Number of shrinks skipped due to safety checks.
        /// </summary>
        public System.Int32 ShrinkSkipped;

        /// <summary>
        /// Number of successful expand operations.
        /// </summary>
        public System.Int32 ExpandAttempted;

        /// <summary>
        /// Last timestamp when pool state changed.
        /// </summary>
        public System.Int64 LastChangeTime;
    }

    #endregion Nested Types

    #region Properties

    /// <summary>Gets the largest buffer size from the buffer allocations list.</summary>
    public System.Int32 MaxBufferSize { get; }

    /// <summary>Gets the smallest buffer size from the buffer allocations list.</summary>
    public System.Int32 MinBufferSize { get; }

    /// <summary>
    /// Gets the recurring name used for buffer trimming operations.
    /// </summary>
    public static readonly System.String RecurringName;

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
    public BufferPoolManager(BufferConfig? bufferConfig = null)
    {
        BufferConfig config = bufferConfig ?? ConfigurationManager.Instance.Get<BufferConfig>();
        config.Validate();

        _config = config;

        _suitablePoolSizeCache = new();
        _metricsCache = new();
        _shrinkPolicy = new ShrinkSafetyPolicy();

        _bufferAllocations = BufferConfig.ParseBufferAllocations(config.BufferAllocations);

        MinBufferSize = System.Linq.Enumerable.Min(_bufferAllocations, alloc => alloc.BufferSize);
        MaxBufferSize = System.Linq.Enumerable.Max(_bufferAllocations, alloc => alloc.BufferSize);

        _poolManager = new BufferPoolCollection(bufferConfig: config);
        _poolManager.EventShrink += SHRINK_BUFFER_POOL_SIZE;
        _poolManager.EventIncrease += INCREASE_BUFFER_POOL_SIZE;

        ALLOCATE_BUFFERS();

        if (_config.EnableMemoryTrimming)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: TaskNaming.Recurring.CleanupJobId(RecurringName, this.GetHashCode()),
                interval: System.TimeSpan.FromMinutes(System.Math.Max(1, _config.TrimIntervalMinutes)),
                work: _ =>
                {
                    TRIM_EXCESS_BUFFERS(null);
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    Jitter = System.TimeSpan.FromSeconds(5),
                    ExecutionTimeout = System.TimeSpan.FromSeconds(5),
                    BackoffCap = System.TimeSpan.FromMinutes(1)
                }
            );
        }
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Rents a buffer of at least the requested size with optimized caching and optional fallback.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Byte[] Rent([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 minimumLength = 256)
    {
        if (IS_FAST_COMMON_SIZE(minimumLength))
        {
            return _poolManager.RentBuffer(minimumLength);
        }

        if (_suitablePoolSizeCache.TryGetValue(minimumLength, out System.Int32 cachedPoolSize))
        {
            return _poolManager.RentBuffer(cachedPoolSize);
        }

        try
        {
            return RENT_FROM_POOLS_WITH_CACHING(minimumLength);
        }
        catch (System.ArgumentException ex)
        {
            return HANDLE_RENT_FAILURE(minimumLength, ex);
        }
    }

    /// <summary>
    /// Returns the buffer to the appropriate pool with safety checks and fallback.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public void Return(System.Byte[]? array, System.Boolean arrayClear = false)
    {
        if (array is null)
        {
            return;
        }

        try
        {
            RETURN_TO_MANAGED_POOLS(array);
        }
        catch (System.ArgumentException ex)
        {
            HANDLE_RETURN_FAILURE(array, ex);
        }
    }

    /// <summary>
    /// Rents a buffer and wraps it as an <see cref="System.ArraySegment{T}"/> for use with
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> (SAEA) workflows.
    /// </summary>
    /// <param name="size">The minimum number of bytes required.</param>
    /// <returns>
    /// An <see cref="System.ArraySegment{T}"/> backed by a pooled buffer,
    /// with <c>Offset = 0</c> and <c>Count = size</c>.
    /// </returns>
    /// <remarks>
    /// The caller must return the underlying array via <see cref="Return(System.ArraySegment{System.Byte})"/>
    /// or <see cref="ReturnFromSaea"/> after use to avoid leaking pool buffers.
    /// </remarks>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.ArraySegment<System.Byte> RentSegment(System.Int32 size = 256)
    {
        System.Byte[] buffer = this.Rent(size);
        return new System.ArraySegment<System.Byte>(buffer, 0, size);
    }

    /// <summary>
    /// Returns a buffer that was rented via <see cref="RentSegment"/> back to the pool.
    /// Only the underlying <see cref="System.ArraySegment{T}.Array"/> is returned;
    /// <c>Offset</c> and <c>Count</c> are ignored.
    /// </summary>
    /// <param name="segment">The segment whose backing array will be returned.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return(System.ArraySegment<System.Byte> segment) => this.Return(segment.Array);

    /// <summary>
    /// Rents a buffer from the pool and assigns it to the given
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> via
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs.SetBuffer(System.Byte[], System.Int32, System.Int32)"/>.
    /// </summary>
    /// <param name="saea">The <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> to configure.</param>
    /// <param name="size">The minimum buffer size required.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="saea"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Call <see cref="ReturnFromSaea"/> when the async operation completes to return the buffer to the pool.
    /// </remarks>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RentForSaea(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.SocketAsyncEventArgs saea,
        System.Int32 size = 256)
    {
        System.ArgumentNullException.ThrowIfNull(saea);

        System.Byte[] buffer = this.Rent(size);
        saea.SetBuffer(buffer, 0, size);
    }

    /// <summary>
    /// Returns the buffer currently assigned to a
    /// <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> back to the pool
    /// and clears the buffer reference on the SAEA.
    /// </summary>
    /// <param name="saea">The <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> whose buffer will be returned.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="saea"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ReturnFromSaea(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.SocketAsyncEventArgs saea)
    {
        System.ArgumentNullException.ThrowIfNull(saea);

        // Grab the buffer before clearing the reference
        System.Byte[]? buffer = saea.Buffer;

        // Detach buffer from SAEA to avoid accidental reuse after return
        saea.SetBuffer(null, 0, 0);

        this.Return(buffer);
    }

    /// <summary>
    /// Gets the allocation ratio for a given buffer size with caching for performance.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Double GetAllocationForSize([System.Diagnostics.CodeAnalysis.NotNull] System.Int32 size)
    {
        if (size > MaxBufferSize)
        {
            return System.Linq.Enumerable.Last(_bufferAllocations).Allocation;
        }

        if (size <= MinBufferSize)
        {
            return System.Linq.Enumerable.First(_bufferAllocations).Allocation;
        }

        System.Int32 left = 0;
        System.Int32 right = _bufferAllocations.Length - 1;

        while (left <= right)
        {
            System.Int32 mid = left + ((right - left) / 2);
            System.Int32 midSize = _bufferAllocations[mid].BufferSize;

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
                                                : System.Linq.Enumerable.Last(_bufferAllocations).Allocation;
    }

    /// <summary>
    /// Generates a report on the current state of the buffer pools with metrics.
    /// </summary>
    /// <returns>A string containing the report.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();

        APPEND_REPORT_HEADER(sb);
        APPEND_REPORT_POOL_DETAILS(sb);
        APPEND_REPORT_METRICS(sb);

        return sb.ToString();
    }

    #endregion Public API

    #region Private: Rent / Return helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IS_FAST_COMMON_SIZE(System.Int32 size) => size is 256 or 512 or 1024 or 2048 or 4096;

    /// <summary>
    /// Rents buffer from configured pools and optionally updates size cache.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Byte[] RENT_FROM_POOLS_WITH_CACHING(System.Int32 size)
    {
        System.Byte[] buffer = _poolManager.RentBuffer(size);

        if (_config.EnableAnalytics)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(BufferPoolManager)}:Internal] rent-fast minimumLength={size}");
        }

        CACHE_SUITABLE_POOL_SIZE(size, buffer.Length);

        return buffer;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CACHE_SUITABLE_POOL_SIZE(System.Int32 requestedSize, System.Int32 actualSize)
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Byte[] HANDLE_RENT_FAILURE(System.Int32 size, System.ArgumentException ex)
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void RETURN_TO_MANAGED_POOLS(System.Byte[] buffer)
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void HANDLE_RETURN_FAILURE(System.Byte[] buffer, System.ArgumentException ex)
    {
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

        if (_config.FallbackToArrayPool)
        {
            if (_config.SecureClear)
            {
                System.Array.Clear(buffer, 0, buffer.Length);
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

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ALLOCATE_BUFFERS()
    {
        if (_isInitialized)
        {
            return;
        }

        foreach (var (bufferSize, allocation) in _bufferAllocations)
        {
            System.Int32 capacity = System.Math.Max(1, (System.Int32)(_config.TotalBuffers * allocation));
            _poolManager.CreatePool(bufferSize, capacity);
        }

        _isInitialized = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                      $"init-ok total={_config.TotalBuffers} pools={_bufferAllocations.Length} min={MinBufferSize} max={MaxBufferSize}");
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void TRIM_EXCESS_BUFFERS(System.Object? _)
    {
        System.Int32 cycle = System.Threading.Interlocked.Increment(ref _trimCycleCount);
        System.Boolean deepTrim = SHOULD_RUN_DEEP_TRIM(cycle);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] trim-run deep={deepTrim}");

        // Compute memory budget once per cycle (cache it)
        (System.Int64 targetBudget, System.Int64 currentUsage, System.Boolean overBudget) = COMPUTE_MEMORY_BUDGET();

        foreach (var pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            // Only consider TRIM logic, NOT auto-expand events (separate concern)
            if (!SHOULD_TRIM_POOL(in info, overBudget, deepTrim))
            {
                continue;
            }

            System.Int32 shrinkStep = CALCULATE_SAFE_SHRINK_STEP(in info, cycle);
            if (shrinkStep <= 0)
            {
                continue;
            }

            TRIM_SINGLE_POOL(pool, in info, shrinkStep);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean SHOULD_RUN_DEEP_TRIM(System.Int32 cycle)
    {
        System.Int32 deepEvery = System.Math.Max(1, _config.DeepTrimIntervalMinutes / System.Math.Max(1, _config.TrimIntervalMinutes));
        return (cycle % deepEvery) == 0;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private (System.Int64 TargetBudget, System.Int64 CurrentUsage, System.Boolean OverBudget) COMPUTE_MEMORY_BUDGET()
    {
        // Cache memory budget for 10 seconds to avoid repeated computation
        System.Int64 now = System.Environment.TickCount64;
        const System.Int64 CacheDurationMs = 10_000;

        if (now - _lastBudgetComputeTime < CacheDurationMs && _cachedMemoryBudget > 0)
        {
            // Return cached value
            System.Int64 current = 0;
            foreach (var pool in _poolManager.GetAllPools())
            {
                ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
                current += info.TotalBuffers * (System.Int64)info.BufferSize;
            }

            return (_cachedMemoryBudget, current, current > _cachedMemoryBudget);
        }

        System.Int64 totalAvailable = System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        System.Int64 percentBudget = (System.Int64)(totalAvailable * _config.MaxMemoryPercentage);
        System.Int64 hardCap = _config.MaxMemoryBytes > 0 ? _config.MaxMemoryBytes : System.Int64.MaxValue;

        System.Int64 targetBudget = System.Math.Min(percentBudget, hardCap);

        _lastBudgetComputeTime = now;
        _cachedMemoryBudget = targetBudget;

        System.Int64 currentUsage = 0;
        foreach (var pool in _poolManager.GetAllPools())
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();
            currentUsage += info.TotalBuffers * (System.Int64)info.BufferSize;
        }

        return (targetBudget, currentUsage, currentUsage > targetBudget);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean SHOULD_TRIM_POOL(in BufferPoolState info, System.Boolean overBudget, System.Boolean deepTrim)
    {
        // Skip if very low idle time
        System.Double usage = info.GetUsageRatio();
        if (usage > 0.95 && !overBudget && !deepTrim)
        {
            return false;
        }

        System.Boolean candidateByFree = info.FreeBuffers >= (System.Int32)(info.TotalBuffers * 0.50);
        System.Boolean candidateByOverBudget = overBudget || deepTrim;

        return candidateByFree || candidateByOverBudget;
    }

    /// <summary>
    /// Calculates shrink step with safety guardrails to prevent aggressive reduction.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private System.Int32 CALCULATE_SAFE_SHRINK_STEP(in BufferPoolState info, System.Int32 cycle)
    {
        if (info.TotalBuffers <= 0)
        {
            return 0;
        }

        // 1. Calculate target based on allocation ratio
        System.Double targetAllocation = GetAllocationForSize(info.BufferSize);
        System.Int32 targetBuffers = (System.Int32)System.Math.Max(
            _shrinkPolicy.AbsoluteMinimum,
            targetAllocation * _config.TotalBuffers
        );

        // 2. Enforce minimum retention percentage (default 25%)
        System.Int32 minimumRetain = (System.Int32)System.Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = System.Math.Max(targetBuffers, minimumRetain);

        // 3. Calculate excess buffers
        System.Int32 excessBuffers = info.FreeBuffers - targetBuffers;
        if (excessBuffers <= 0)
        {
            return 0;
        }

        // 4. Apply multiple safety limits
        System.Int32 maxPerCycle = (System.Int32)System.Math.Ceiling(
            info.TotalBuffers * _shrinkPolicy.MaxShrinkPercentPerCycle
        );

        System.Int32 shrinkStep = System.Math.Min(excessBuffers, maxPerCycle);
        shrinkStep = System.Math.Min(shrinkStep, _shrinkPolicy.MaxSingleShrinkStep);

        return System.Math.Max(0, shrinkStep);
    }

    /// <summary>
    /// Applies trim on a single pool with metrics tracking.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void TRIM_SINGLE_POOL(BufferPoolShared pool, in BufferPoolState info, System.Int32 shrinkStep)
    {
        System.Double usage = info.GetUsageRatio();

        pool.DecreaseCapacity(shrinkStep);

        // Track metrics
        if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
        {
            metrics.TotalBytesReturned += (System.Int64)shrinkStep * info.BufferSize;
            metrics.ShrinkAttempted++;
            metrics.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[info.BufferSize] = metrics;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                      $"trim-shrink minimumLength={info.BufferSize} step={shrinkStep} usage={usage:F2}% " +
                                      $"remain={info.TotalBuffers - shrinkStep}");
    }

    #endregion Private: Allocation & Trimming

    #region Private: Resize Strategies

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean IS_OVER_MEMORY_BUDGET()
    {
        var (_, _, overBudget) = COMPUTE_MEMORY_BUDGET();
        return overBudget;
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void SHRINK_BUFFER_POOL_SIZE(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        System.Int32 buffersToShrink = CALCULATE_AUTO_SHRINK_AMOUNT(in poolInfo);
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
            m.TotalBytesReturned += (System.Int64)buffersToShrink * poolInfo.BufferSize;
            m.ShrinkAttempted++;
            m.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = m;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] shrink minimumLength={latest.BufferSize} by={buffersToShrink}");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CALCULATE_AUTO_SHRINK_AMOUNT(in BufferPoolState poolInfo)
    {
        if (poolInfo.TotalBuffers <= 0)
        {
            return 0;
        }

        System.Double targetAllocation = GetAllocationForSize(poolInfo.BufferSize);
        System.Int32 targetBuffers = (System.Int32)(targetAllocation * _config.TotalBuffers);

        System.Int32 minimumRetain = (System.Int32)System.Math.Ceiling(
            poolInfo.TotalBuffers * _shrinkPolicy.MinimumRetentionPercent
        );
        targetBuffers = System.Math.Max(targetBuffers, minimumRetain);

        System.Int32 excessBuffers = poolInfo.FreeBuffers - targetBuffers;
        return System.Math.Clamp(excessBuffers, 0, _config.MaxBufferIncreaseLimit);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void INCREASE_BUFFER_POOL_SIZE(BufferPoolShared pool)
    {
        ref readonly BufferPoolState poolInfo = ref pool.GetPoolInfoRef();

        System.Int32 threshold = System.Math.Max(1, (System.Int32)(poolInfo.TotalBuffers * 0.25));
        if (poolInfo.FreeBuffers > threshold)
        {
            return;
        }

        System.Double usage = poolInfo.GetUsageRatio();
        System.Double missRatio = poolInfo.GetMissRate();

        System.Int32 increaseStep = CALCULATE_INCREASE_STEP(in poolInfo, usage, missRatio);
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
            metrics.LastChangeTime = System.Environment.TickCount64;
            _metricsCache[poolInfo.BufferSize] = metrics;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[SH.{nameof(BufferPoolManager)}:Internal] " +
                                       $"increase minimumLength={poolInfo.BufferSize} by={increaseStep} usage={usage:F2}% miss={missRatio:F2}%");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CALCULATE_INCREASE_STEP(in BufferPoolState poolInfo, System.Double usage, System.Double missRatio)
    {
        System.Int32 baseIncreasePow2 = System.Math.Max(_config.MinimumIncrease,
                (System.Int32)System.Numerics.BitOperations.RoundUpToPowerOf2(
                    (System.UInt32)System.Math.Max(1, poolInfo.TotalBuffers >> 2)));

        System.Double usageFactor = 1.0 + System.Math.Max(0.0, (usage - 0.75) * 2.0);
        System.Double missFactor = 1.0 + System.Math.Min(1.0, missRatio * 2.0);

        System.Int32 scaled = (System.Int32)System.Math.Ceiling(
            baseIncreasePow2 * usageFactor * missFactor * _config.AdaptiveGrowthFactor);

        // Tính soft cap: tối đa 25% TotalBuffers hiện tại mỗi lần expand
        // Tránh spike lớn khi pool đang nhỏ mà miss rate đột ngột cao
        System.Int32 softCap = System.Math.Max(
            _config.MinimumIncrease,
            (System.Int32)System.Math.Ceiling(poolInfo.TotalBuffers * 0.25));

        return System.Math.Min(scaled, System.Math.Min(softCap, _config.MaxBufferIncreaseLimit));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean SHOULD_APPLY_SHRINK(in BufferPoolState poolInfo, System.Int32 buffersToShrink) => buffersToShrink > 0 && poolInfo.FreeBuffers >= buffersToShrink;

    #endregion Private: Resize Strategies

    #region Private: Reporting

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_HEADER(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] BufferPoolManager Status:");
        _ = sb.AppendLine($"Initialized: {_isInitialized}");
        _ = sb.AppendLine($"Total Buffers (Configured): {_config.TotalBuffers}");
        _ = sb.AppendLine($"Pools: {_bufferAllocations.Length}");
        _ = sb.AppendLine($"Min Buffer SIZE: {MinBufferSize}");
        _ = sb.AppendLine($"Max Buffer SIZE: {MaxBufferSize}");
        _ = sb.AppendLine($"Enable Trimming: {_config.EnableMemoryTrimming}");
        _ = sb.AppendLine($"Enable Analytics: {_config.EnableAnalytics}");
        _ = sb.AppendLine($"Enable SecureClear: {_config.SecureClear}");
        _ = sb.AppendLine($"Fallback to ArrayPool: {_config.FallbackToArrayPool}");
        _ = sb.AppendLine($"Trim Interval (min): {_config.TrimIntervalMinutes}");
        _ = sb.AppendLine($"Deep Trim Interval (min): {_config.DeepTrimIntervalMinutes}");
        _ = sb.AppendLine($"Trim Cycles Run: {_trimCycleCount}");
        _ = sb.AppendLine();
        _ = sb.AppendLine("Shrink Safety Policy:");
        _ = sb.AppendLine($"  Minimum Retention: {_shrinkPolicy.MinimumRetentionPercent * 100:F1}%");
        _ = sb.AppendLine($"  Max Single Shrink Step: {_shrinkPolicy.MaxSingleShrinkStep}");
        _ = sb.AppendLine($"  Max Shrink Per Cycle: {_shrinkPolicy.MaxShrinkPercentPerCycle * 100:F1}%");
        _ = sb.AppendLine();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_POOL_DETAILS(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine("s_pool Details:");
        _ = sb.AppendLine("----------------------------------------------------------------------");
        _ = sb.AppendLine("SIZE     | Total  | Free   | In Use  | Usage %  | MissRate");
        _ = sb.AppendLine("----------------------------------------------------------------------");

        foreach (var pool in System.Linq.Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            System.Int32 inUse = info.TotalBuffers - info.FreeBuffers;
            System.Double usage = info.GetUsageRatio() * 100.0;
            System.Double miss = info.GetMissRate() * 100.0;

            _ = sb.AppendLine($"{info.BufferSize,8} | {info.TotalBuffers,6} | {info.FreeBuffers,5} | {inUse,7} | {usage,8:F2}% | {miss,7:F2}%");
        }

        _ = sb.AppendLine("----------------------------------------------------------------------");
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void APPEND_REPORT_METRICS(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine();
        _ = sb.AppendLine("s_pool Metrics (Shrink/Expand Operations):");
        _ = sb.AppendLine("----------------------------------------------------------------------");
        _ = sb.AppendLine("SIZE     | Shrink OK | Shrink Skip | Expand OK | Bytes Returned");
        _ = sb.AppendLine("----------------------------------------------------------------------");

        foreach (var pool in System.Linq.Enumerable.OrderBy(_poolManager.GetAllPools(), p => p.GetPoolInfoRef().BufferSize))
        {
            ref readonly BufferPoolState info = ref pool.GetPoolInfoRef();

            if (_metricsCache.TryGetValue(info.BufferSize, out BufferPoolMetrics metrics))
            {
                System.String bytesReturnedStr = metrics.TotalBytesReturned > 1_000_000
                    ? $"{metrics.TotalBytesReturned / 1_000_000}MB"
                    : $"{metrics.TotalBytesReturned / 1024}KB";

                _ = sb.AppendLine($"{info.BufferSize,8} | {metrics.ShrinkAttempted,9} | {metrics.ShrinkSkipped,11} | {metrics.ExpandAttempted,9} | {bytesReturnedStr,14}");
            }
            else
            {
                _ = sb.AppendLine($"{info.BufferSize,8} | {0,9} | {0,11} | {0,9} | {"0KB",14}");
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
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelRecurring(TaskNaming.Recurring
                                    .CleanupJobId(RecurringName, this.GetHashCode()));
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(BufferPoolManager)}:{nameof(Dispose)}] disposed");

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}