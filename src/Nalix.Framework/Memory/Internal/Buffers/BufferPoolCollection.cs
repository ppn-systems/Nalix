// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;

namespace Nalix.Framework.Memory.Internal.Buffers;

/// <summary>
/// Describes the direction of a buffer pool resize event.
/// </summary>
public enum BufferPoolResizeDirection
{
    /// <summary>
    /// Indicates the pool is expanding capacity.
    /// </summary>
    Increase = 0,
    /// <summary>
    /// Indicates the pool is reducing capacity.
    /// </summary>
    Shrink = 1,
}

/// <summary>
/// Manages shared buffer pools and emits resize signals based on observed usage.
/// </summary>
[DebuggerNonUserCode]
[DebuggerDisplay("Pools={_pools.Count}, Keys={_sortedKeys.Length}")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class BufferPoolCollection : IDisposable
{
    #region Fields

    private const int MinCooldownMs = 500;
    private const int MaxCooldownMs = 10_000;

    /// <summary>
    /// hysteresis
    /// </summary>
    private const double UsageEpsilon = 0.05;

    private readonly ReaderWriterLockSlim _keysLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, BufferPoolShared> _pools;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, long> _cooldowns;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, BufferCounters> _adjustmentCounters;

    private readonly BufferConfig _config;
    private readonly int _autoTuneThreshold;

    private int[] _sortedKeys;

    #endregion Fields

    #region Nested Types

    /// <summary>
    /// Thread-safe counters used to debounce resize events.
    /// </summary>
    private sealed class BufferCounters
    {
        public int RentCounter;
        public int ReturnCounter;
    }

    #endregion Nested Types

    #region Events

    /// <summary>
    /// Event triggered when buffer pool needs to resize.
    /// </summary>
    public event Action<BufferPoolShared, BufferPoolResizeDirection>? ResizeOccurred;

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
        _autoTuneThreshold = Math.Max(10, _config.AutoTuneOperationThreshold);

        _pools = new();
        _cooldowns = new();
        _adjustmentCounters = new();
        _keysLock = new(LockRecursionPolicy.NoRecursion);
    }

    #endregion Constructor

    #region Pool Management

    /// <summary>
    /// Creates a new buffer pool with a specified buffer size and initial capacity.
    /// </summary>
    /// <param name="bufferSize">The buffer size for the new pool.</param>
    /// <param name="initialCapacity">The number of buffers to preallocate.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CreatePool(int bufferSize, int initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity)))
        {
            this.UpdateSortedKeys();
        }
    }

    /// <summary>
    /// Returns a snapshot enumeration of all pools. The collection may change over time.
    /// </summary>
    public IReadOnlyCollection<BufferPoolShared> GetAllPools()
        => [.. _pools.Values]; // real snapshot, avoids invalid cast

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetPool(int bufferSize, [NotNullWhen(true)] out BufferPoolShared? pool)
        => _pools.TryGetValue(bufferSize, out pool);

    #endregion Pool Management

    #region Rent/Return

    /// <summary>
    /// Rents a buffer that is at least the requested size with optimized lookup.
    /// </summary>
    /// <param name="size">The minimum buffer size required.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="size"/> exceeds every configured pool size.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the selected pool key no longer resolves to a backing pool.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] RentBuffer(int size)
    {
        int initialPoolSize = this.FindSuitablePoolSize(size);
        if (initialPoolSize == 0)
        {
            throw new ArgumentException($"Requested size too large: size={size}");
        }

        // We try to satisfy the request from our managed pools starting from 
        // the most suitable size and moving upwards (Escalation Renting).
        // This avoids immediate fallback to ArrayPool.Shared if a specific batch is exhausted.
        _keysLock.EnterReadLock();
        try
        {
            int[] keys = _sortedKeys;
            int startIndex = Array.BinarySearch(keys, initialPoolSize);
            if (startIndex < 0)
            {
                startIndex = ~startIndex;
            }

            for (int i = startIndex; i < keys.Length; i++)
            {
                int currentPoolSize = keys[i];
                if (_pools.TryGetValue(currentPoolSize, out BufferPoolShared? pool))
                {
                    // Attempt to take from this pool without a fallback.
                    if (pool.TryAcquireBuffer(out byte[]? buffer))
                    {
                        if (this.AdjustCounter(currentPoolSize, isRent: true))
                        {
                            this.EvaluateResize(pool);
                        }
                        return buffer;
                    }
                }
            }
        }
        finally
        {
            _keysLock.ExitReadLock();
        }

        // If we reach here, ALL suitable managed pools are exhausted.
        // Falls back to the shared ArrayPool via the most suitable pool manager to track the miss.
        if (_pools.TryGetValue(initialPoolSize, out BufferPoolShared? originalPool))
        {
            // Even if we miss, we must signal that this pool is under pressure
            // so the auto-resize logic can grow it to meet future demand.
            if (this.AdjustCounter(initialPoolSize, isRent: true))
            {
                this.EvaluateResize(originalPool);
            }

            return originalPool.AcquireBuffer();
        }

        throw new InvalidOperationException($"Buffer pool not found for size {initialPoolSize}.");
    }

    /// <summary>
    /// Returns a buffer to the appropriate pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="buffer"/> does not match any managed pool size.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnBuffer(byte[]? buffer)
    {
        if (buffer is null)
        {
            return;
        }

        if (!_pools.TryGetValue(buffer.Length, out BufferPoolShared? pool))
        {
            throw new ArgumentException($"Invalid buffer size: {buffer.Length}.");
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
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateSortedKeys()
    {
        _keysLock.EnterWriteLock();
        try
        {
            _sortedKeys = [.. Enumerable.Order(_pools.Keys)];
        }
        finally
        {
            _keysLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Orchestrates auto-resize evaluation for a given pool.
    /// </summary>
    /// <param name="pool">The pool to evaluate.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EvaluateResize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState state = ref pool.GetPoolInfoRef();
        double usage = state.GetUsageRatio();
        double missRate = state.GetMissRate();

        long now = System.Environment.TickCount64;
        int cooldown = GetAdaptiveCooldown(usage, missRate);

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
    /// <param name="usage">The current usage ratio.</param>
    /// <param name="missRate">The current miss rate.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetAdaptiveCooldown(double usage, double missRate)
    {
        if (usage >= 0.90 || missRate >= 0.10)
        {
            // SYSTEM is hot -> react quickly
            return MinCooldownMs;
        }

        if (usage <= 0.20 && missRate < 0.01)
        {
            // Very idle -> reduce resize frequency
            return MaxCooldownMs;
        }

        // Default
        return 3_000;
    }

    /// <summary>
    /// Checks whether we are still in cooldown window for a buffer size.
    /// </summary>
    /// <param name="bufferSize">The buffer size being checked.</param>
    /// <param name="now">The current time in tick count milliseconds.</param>
    /// <param name="cooldown">The cooldown duration in milliseconds.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCooldownActive(int bufferSize, long now, int cooldown)
    {
        long last = _cooldowns.GetOrAdd(bufferSize, 0);
        return now - last < cooldown;
    }

    /// <summary>
    /// Attempts to expand the pool when under pressure.
    /// </summary>
    /// <param name="pool">The pool to expand.</param>
    /// <param name="state">The current pool state snapshot.</param>
    /// <param name="usage">The current usage ratio.</param>
    /// <param name="missRate">The current miss rate.</param>
    /// <param name="now">The current time in tick count milliseconds.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryExpandPool(
        BufferPoolShared pool,
        in BufferPoolState state,
        double usage,
        double missRate,
        long now)
    {
        double expandThreshold = _config.ExpandThresholdPercent;
        if (usage < (expandThreshold + UsageEpsilon) && missRate <= 0.02)
        {
            return false;
        }

        int step = this.CalculateExpandStep(state, usage, expandThreshold);
        if (step <= 0)
        {
            return false;
        }

        ResizeOccurred?.Invoke(pool, BufferPoolResizeDirection.Increase);
        pool.IncreaseCapacity(step);
        _cooldowns[state.BufferSize] = now;

        return true;
    }

    /// <summary>
    /// Calculates expansion step based on usage and configuration.
    /// </summary>
    /// <param name="state">The current pool state snapshot.</param>
    /// <param name="usage">The current usage ratio.</param>
    /// <param name="expandThreshold">The configured expand threshold.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateExpandStep(in BufferPoolState state, double usage, double expandThreshold)
    {
        int minIncrease = _config.MinimumIncrease;
        int maxOneShot = _config.MaxBufferIncreaseLimit;

        int _ = state.TotalBuffers <= 0 ? 1 : state.TotalBuffers;
        int pressure = Math.Max(
            1,
            (int)Math.Ceiling((usage - expandThreshold) * 8)
        );

        return Math.Clamp(pressure * minIncrease, minIncrease, maxOneShot);
    }

    /// <summary>
    /// Attempts to shrink the pool when it is sufficiently idle.
    /// </summary>
    /// <param name="pool">The pool to shrink.</param>
    /// <param name="state">The current pool state snapshot.</param>
    /// <param name="usage">The current usage ratio.</param>
    /// <param name="now">The current time in tick count milliseconds.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TryShrinkPool(
        BufferPoolShared pool,
        in BufferPoolState state,
        double usage,
        long now)
    {
        double shrinkThreshold = _config.ShrinkThresholdPercent;

        if (usage > (shrinkThreshold - UsageEpsilon) || !state.CanShrink)
        {
            return;
        }

        int free = state.FreeBuffers;
        if (free <= 0)
        {
            return;
        }

        int minIncrease = _config.MinimumIncrease;
        int step = Math.Max(minIncrease, free / 4); // conservative

        ResizeOccurred?.Invoke(pool, BufferPoolResizeDirection.Shrink);
        pool.DecreaseCapacity(step);
        _cooldowns[state.BufferSize] = now;
    }

    /// <summary>
    /// Adjusts the rent and return counters and decides whether to trigger a resize evaluation.
    /// </summary>
    /// <param name="poolSize">The pool size being tracked.</param>
    /// <param name="isRent">Whether the counter represents a rent operation.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool AdjustCounter(int poolSize, bool isRent)
    {
        BufferCounters counters = _adjustmentCounters.GetOrAdd(poolSize, static _ => new BufferCounters());

        ref int counterRef = ref (isRent
            ? ref counters.RentCounter
            : ref counters.ReturnCounter);

        int newValue = Interlocked.Increment(ref counterRef);
        if (newValue >= _autoTuneThreshold)
        {
            _ = Interlocked.Exchange(ref counterRef, 0);
            return true;
        }

        return false;
    }

    #endregion Resize Debounce

    #region Lookup

    /// <summary>
    /// Finds the most suitable pool size with optimized binary search.
    /// </summary>
    /// <param name="size">The requested buffer size.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindSuitablePoolSize(int size)
    {
        _keysLock.EnterReadLock();
        try
        {
            Span<int> keys = MemoryExtensions.AsSpan(_sortedKeys);

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

            int index = MemoryExtensions.BinarySearch(keys, size);

            if (index >= 0)
            {
                return keys[index];
            }
            else if (~index < keys.Length)
            {
                return keys[~index];
            }
            else
            {
                return 0;
            }
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
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        _ = Parallel.ForEach(_pools.Values, pool => pool.Dispose());

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

        GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}
