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

    private const System.Int32 CooldownMs = 3_000; // avoid thrash
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
    /// <param name="bufferConfig">Whether buffers should be wiped to zero when returned/disposed.</param>
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
        => (System.Collections.Generic.IReadOnlyCollection<BufferPoolShared>)_pools.Values;

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
            _sortedKeys = [.. System.Linq.Enumerable.OrderBy(_pools.Keys, k => k)];
        }
        finally
        {
            _keysLock.ExitWriteLock();
        }
    }

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

        if (AdjustCounter(poolSize, isRent: true))
        {
            // Pull config thresholds from your BufferConfig holder (inject or pass in ctor)
            EvaluateResize(
                pool,
                expandThreshold: _config.ExpandThresholdPercent,   // or cfg.ExpandThresholdPercent
                shrinkThreshold: _config.ShrinkThresholdPercent,   // or cfg.ShrinkThresholdPercent
                minIncrease: _config.MinimumIncrease,          // or cfg.MinimumIncrease
                maxOneShot: _config.MaxBufferIncreaseLimit         // or cfg.MaxBufferIncreaseLimit
            );
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

        if (AdjustCounter(buffer.Length, isRent: false))
        {
            EvaluateResize(
                pool,
                expandThreshold: _config.ExpandThresholdPercent,   // or cfg.ExpandThresholdPercent
                shrinkThreshold: _config.ShrinkThresholdPercent,   // or cfg.ShrinkThresholdPercent
                minIncrease: _config.MinimumIncrease,          // or cfg.MinimumIncrease
                maxOneShot: _config.MaxBufferIncreaseLimit         // or cfg.MaxBufferIncreaseLimit
            );
        }
    }

    #endregion Rent/Return

    #region Resize Debounce

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void EvaluateResize(
        BufferPoolShared pool, System.Double expandThreshold,
        System.Double shrinkThreshold, System.Int32 minIncrease, System.Int32 maxOneShot)
    {
        ref readonly BufferPoolState st = ref pool.GetPoolInfoRef(); // cheap snapshot
        System.Double usage = st.GetUsageRatio(); // 0..1 (1 = hot)
        System.Double missRate = st.GetMissRate();

        // Do not resize too frequently (per pool)
        System.Int64 now = System.Environment.TickCount64;
        System.Int64 last = _cooldowns.GetOrAdd(st.BufferSize, 0);
        if (now - last < CooldownMs)
        {
            return;
        }

        // Expand when "hot" with safety margin; also consider misses
        if (usage >= (expandThreshold + UsageEpsilon) || missRate > 0.02)
        {
            System.Int32 total = st.TotalBuffers <= 0 ? 1 : st.TotalBuffers;
            System.Int32 pressure = System.Math.Max(1, (System.Int32)System.Math.Ceiling((usage - expandThreshold) * 8)); // rough proportional
            System.Int32 step = System.Math.Clamp(pressure * minIncrease, minIncrease, maxOneShot);
            EventIncrease?.Invoke(pool);
            pool.IncreaseCapacity(step);
            _cooldowns[st.BufferSize] = now;
            return;
        }

        // Shrink only when really idle and safe (free >= shrink threshold)
        if (usage <= (shrinkThreshold - UsageEpsilon) && st.CanShrink)
        {
            System.Int32 free = st.FreeBuffers;
            if (free > 0)
            {
                System.Int32 step = System.Math.Max(minIncrease, free / 4); // conservative
                EventShrink?.Invoke(pool);
                pool.DecreaseCapacity(step);
                _cooldowns[st.BufferSize] = now;
            }
        }
    }

    /// <summary>
    /// Adjusts the rent and return counters and decides whether to fire a resize event.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean AdjustCounter(System.Int32 poolSize, System.Boolean isRent)
    {
        BufferCounters counters = _adjustmentCounters.GetOrAdd(poolSize, static _ => new BufferCounters());

        if (isRent)
        {
            System.Int32 newValue = System.Threading.Interlocked.Increment(ref counters.RentCounter);
            if (newValue >= _autoTuneThreshold)
            {
                _ = System.Threading.Interlocked.Exchange(ref counters.RentCounter, 0);
                return true;
            }
        }
        else
        {
            System.Int32 newValue = System.Threading.Interlocked.Increment(ref counters.ReturnCounter);
            if (newValue >= _autoTuneThreshold)
            {
                _ = System.Threading.Interlocked.Exchange(ref counters.ReturnCounter, 0);
                return true;
            }
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