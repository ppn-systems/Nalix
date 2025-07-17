// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Manages shared buffer pools and emits resize signals based on observed usage.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.DebuggerDisplay("Pools={_pools.Count}, Keys={_sortedKeys.Length}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class BufferPoolCollection : System.IDisposable
{
    #region Fields

    private const System.Int32 MinCooldownMs = 500;
    private const System.Int32 MaxCooldownMs = 10_000;
    private const System.Double UsageEpsilon = 0.05; // hysteresis

    private readonly System.Threading.ReaderWriterLockSlim _keysLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, BufferPoolShared> _pools;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, System.Int64> _cooldowns;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, BufferCounters> _adjustmentCounters;

    private readonly BufferConfig _config;
    private readonly System.Int32 _autoTuneThreshold;

    private System.Int32[] _sortedKeys;

    #endregion Fields

    #region Nested Types

    /// <summary>
    /// Thread-safe counters used to debounce resize events.
    /// </summary>
    private sealed class BufferCounters
    {
        public System.Int32 RentCounter;
        public System.Int32 ReturnCounter;
    }

    #endregion Nested Types

    #region Events

    /// <summary>
    /// Event triggered when buffer pool needs to increase capacity.
    /// </summary>
    public event System.Action<BufferPoolShared>? EventIncrease;

    /// <summary>
    /// Event triggered when buffer pool needs to decrease capacity.
    /// </summary>
    public event System.Action<BufferPoolShared>? EventShrink;

    #endregion Events

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferPoolCollection"/> class.
    /// </summary>
    /// <param name="bufferConfig">Buffer configuration used for all pools.</param>
    public BufferPoolCollection(BufferConfig bufferConfig)
    {
        _sortedKeys = [];
        _config = bufferConfig;
        _autoTuneThreshold = System.Math.Max(10, _config.AutoTuneOperationThreshold);

        _pools = new();
        _cooldowns = new();
        _adjustmentCounters = new();
        _keysLock = new(System.Threading.LockRecursionPolicy.NoRecursion);
    }

    #endregion Constructor

    #region Pool Management

    /// <summary>
    /// Creates a new buffer pool with a specified buffer size and initial capacity.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void CreatePool(System.Int32 bufferSize, System.Int32 initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity, _config.SecureClear)))
        {
            this.UpdateSortedKeys();
        }
    }

    /// <summary>
    /// Returns a snapshot enumeration of all pools. The collection may change over time.
    /// </summary>
    public System.Collections.Generic.IReadOnlyCollection<BufferPoolShared> GetAllPools()
        => [.. _pools.Values]; // real snapshot, avoids invalid cast

    #endregion Pool Management

    #region Rent/Return

    /// <summary>
    /// Rents a buffer that is at least the requested size with optimized lookup.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] RentBuffer(System.Int32 size)
    {
        System.Int32 poolSize = FindSuitablePoolSize(size);
        if (poolSize == 0)
        {
            throw new System.ArgumentException($"Requested buffer size ({size}) exceeds maximum available pool size.");
        }

        if (!_pools.TryGetValue(poolSize, out BufferPoolShared? pool))
        {
            throw new System.InvalidOperationException($"Pools for size {poolSize} is not available.");
        }

        System.Byte[] buffer = pool.AcquireBuffer();

        if (this.AdjustCounter(poolSize, isRent: true))
        {
            this.EvaluateResize(pool);
        }

        return buffer;
    }

    /// <summary>
    /// Returns a buffer to the appropriate pool.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ReturnBuffer(System.Byte[]? buffer)
    {
        if (buffer is null)
        {
            return;
        }

        if (!_pools.TryGetValue(buffer.Length, out BufferPoolShared? pool))
        {
            throw new System.ArgumentException($"Invalid buffer size: {buffer.Length}.");
        }

        pool.ReleaseBuffer(buffer);

        if (this.AdjustCounter(buffer.Length, isRent: false))
        {
            this.EvaluateResize(pool);
        }
    }

    #endregion Rent/Return

    #region Resize Debounce

    /// <summary>
    /// Updates the sorted keys with proper locking.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void UpdateSortedKeys()
    {
        _keysLock.EnterWriteLock();
        try
        {
            _sortedKeys = [.. System.Linq.Enumerable.Order(_pools.Keys)];
        }
        finally
        {
            _keysLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Orchestrates auto-resize evaluation for a given pool.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void EvaluateResize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState state = ref pool.GetPoolInfoRef();
        System.Double usage = state.GetUsageRatio();
        System.Double missRate = state.GetMissRate();

        System.Int64 now = System.Environment.TickCount64;
        System.Int32 cooldown = GetAdaptiveCooldown(usage, missRate);

        if (this.IsCooldownActive(state.BufferSize, now, cooldown))
        {
            return;
        }

        if (this.TryExpandPool(pool, in state, usage, missRate, now))
        {
            return;
        }

        this.TryShrinkPool(pool, in state, usage, now);
    }

    /// <summary>
    /// Calculates cooldown based on current usage and miss rate.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 GetAdaptiveCooldown(System.Double usage, System.Double missRate)
    {
        if (usage >= 0.90 || missRate >= 0.10)
        {
            // SYSTEM is hot → react quickly
            return MinCooldownMs;
        }

        if (usage <= 0.20 && missRate < 0.01)
        {
            // Very idle → reduce resize frequency
            return MaxCooldownMs;
        }

        // Default
        return 3_000;
    }

    /// <summary>
    /// Checks whether we are still in cooldown window for a buffer size.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean IsCooldownActive(System.Int32 bufferSize, System.Int64 now, System.Int32 cooldown)
    {
        System.Int64 last = _cooldowns.GetOrAdd(bufferSize, 0);
        return now - last < cooldown;
    }

    /// <summary>
    /// Attempts to expand the pool when under pressure.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.Boolean TryExpandPool(
        BufferPoolShared pool,
        in BufferPoolState state,
        System.Double usage,
        System.Double missRate,
        System.Int64 now)
    {
        System.Double expandThreshold = _config.ExpandThresholdPercent;
        if (usage < (expandThreshold + UsageEpsilon) && missRate <= 0.02)
        {
            return false;
        }

        System.Int32 step = this.CalculateExpandStep(state, usage, expandThreshold);
        if (step <= 0)
        {
            return false;
        }

        EventIncrease?.Invoke(pool);
        pool.IncreaseCapacity(step);
        _cooldowns[state.BufferSize] = now;

        return true;
    }

    /// <summary>
    /// Calculates expansion step based on usage and configuration.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 CalculateExpandStep(in BufferPoolState state, System.Double usage, System.Double expandThreshold)
    {
        System.Int32 minIncrease = _config.MinimumIncrease;
        System.Int32 maxOneShot = _config.MaxBufferIncreaseLimit;

        System.Int32 _ = state.TotalBuffers <= 0 ? 1 : state.TotalBuffers;
        System.Int32 pressure = System.Math.Max(
            1,
            (System.Int32)System.Math.Ceiling((usage - expandThreshold) * 8)
        );

        return System.Math.Clamp(pressure * minIncrease, minIncrease, maxOneShot);
    }

    /// <summary>
    /// Attempts to shrink the pool when it is sufficiently idle.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void TryShrinkPool(
        BufferPoolShared pool,
        in BufferPoolState state,
        System.Double usage,
        System.Int64 now)
    {
        System.Double shrinkThreshold = _config.ShrinkThresholdPercent;

        if (usage > (shrinkThreshold - UsageEpsilon) || !state.CanShrink)
        {
            return;
        }

        System.Int32 free = state.FreeBuffers;
        if (free <= 0)
        {
            return;
        }

        System.Int32 minIncrease = _config.MinimumIncrease;
        System.Int32 step = System.Math.Max(minIncrease, free / 4); // conservative

        EventShrink?.Invoke(pool);
        pool.DecreaseCapacity(step);
        _cooldowns[state.BufferSize] = now;
    }

    /// <summary>
    /// Adjusts the rent and return counters and decides whether to trigger a resize evaluation.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean AdjustCounter(System.Int32 poolSize, System.Boolean isRent)
    {
        BufferCounters counters = _adjustmentCounters.GetOrAdd(poolSize, static _ => new BufferCounters());

        ref System.Int32 counterRef = ref (isRent
            ? ref counters.RentCounter
            : ref counters.ReturnCounter);

        System.Int32 newValue = System.Threading.Interlocked.Increment(ref counterRef);
        if (newValue >= _autoTuneThreshold)
        {
            _ = System.Threading.Interlocked.Exchange(ref counterRef, 0);
            return true;
        }

        return false;
    }

    #endregion Resize Debounce

    #region Lookup

    /// <summary>
    /// Finds the most suitable pool size with optimized binary search.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 FindSuitablePoolSize(System.Int32 size)
    {
        _keysLock.EnterReadLock();
        try
        {
            System.Span<System.Int32> keys = System.MemoryExtensions.AsSpan(_sortedKeys);

            if (keys.Length == 0)
            {
                return 0;
            }

            if (size <= keys[0])
            {
                return keys[0];
            }

            if (size > keys[^1])
            {
                return 0;
            }

            System.Int32 index = System.MemoryExtensions.BinarySearch(keys, size);

            return index >= 0
                ? keys[index]
                : (~index < keys.Length ? keys[~index] : 0);
        }
        finally
        {
            _keysLock.ExitReadLock();
        }
    }

    #endregion Lookup

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the buffer pools.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        _ = System.Threading.Tasks.Parallel.ForEach(_pools.Values, pool => pool.Dispose());

        _pools.Clear();
        _adjustmentCounters.Clear();

        _keysLock.EnterWriteLock();
        try
        {
            _sortedKeys = [];
        }
        finally
        {
            _keysLock.ExitWriteLock();
            _keysLock.Dispose();
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
