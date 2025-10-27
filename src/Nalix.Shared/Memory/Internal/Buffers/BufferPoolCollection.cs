// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Shared.Memory.Internal.Buffers;

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
    /// Event triggered when buffer pool needs to increase capacity.
    /// </summary>
    public event Action<BufferPoolShared>? EventIncrease;

    /// <summary>
    /// Event triggered when buffer pool needs to decrease capacity.
    /// </summary>
    public event Action<BufferPoolShared>? EventShrink;

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
    /// <param name="bufferSize"></param>
    /// <param name="initialCapacity"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void CreatePool(int bufferSize, int initialCapacity)
    {
        if (_pools.TryAdd(bufferSize, BufferPoolShared.GetOrCreatePool(bufferSize, initialCapacity, _config.SecureClear)))
        {
            UpdateSortedKeys();
        }
    }

    /// <summary>
    /// Returns a snapshot enumeration of all pools. The collection may change over time.
    /// </summary>
    public IReadOnlyCollection<BufferPoolShared> GetAllPools()
        => [.. _pools.Values]; // real snapshot, avoids invalid cast

    #endregion Pool Management

    #region Rent/Return

    /// <summary>
    /// Rents a buffer that is at least the requested size with optimized lookup.
    /// </summary>
    /// <param name="size"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] RentBuffer(int size)
    {
        int poolSize = FindSuitablePoolSize(size);
        if (poolSize == 0)
        {
            throw new ArgumentException($"Requested buffer size ({size}) exceeds maximum available pool size.");
        }

        if (!_pools.TryGetValue(poolSize, out BufferPoolShared? pool))
        {
            throw new InvalidOperationException($"Pools for size {poolSize} is not available.");
        }

        byte[] buffer = pool.AcquireBuffer();

        if (AdjustCounter(poolSize, isRent: true))
        {
            EvaluateResize(pool);
        }

        return buffer;
    }

    /// <summary>
    /// Returns a buffer to the appropriate pool.
    /// </summary>
    /// <param name="buffer"></param>
    /// <exception cref="ArgumentException"></exception>
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

        if (AdjustCounter(buffer.Length, isRent: false))
        {
            EvaluateResize(pool);
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
    /// <param name="pool"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EvaluateResize(BufferPoolShared pool)
    {
        ref readonly BufferPoolState state = ref pool.GetPoolInfoRef();
        double usage = state.GetUsageRatio();
        double missRate = state.GetMissRate();

        long now = Environment.TickCount64;
        int cooldown = GetAdaptiveCooldown(usage, missRate);

        if (IsCooldownActive(state.BufferSize, now, cooldown))
        {
            return;
        }

        if (TryExpandPool(pool, in state, usage, missRate, now))
        {
            return;
        }

        TryShrinkPool(pool, in state, usage, now);
    }

    /// <summary>
    /// Calculates cooldown based on current usage and miss rate.
    /// </summary>
    /// <param name="usage"></param>
    /// <param name="missRate"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetAdaptiveCooldown(double usage, double missRate)
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
    /// <param name="bufferSize"></param>
    /// <param name="now"></param>
    /// <param name="cooldown"></param>
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
    /// <param name="pool"></param>
    /// <param name="state"></param>
    /// <param name="usage"></param>
    /// <param name="missRate"></param>
    /// <param name="now"></param>
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

        int step = CalculateExpandStep(state, usage, expandThreshold);
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
    /// <param name="state"></param>
    /// <param name="usage"></param>
    /// <param name="expandThreshold"></param>
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
    /// <param name="pool"></param>
    /// <param name="state"></param>
    /// <param name="usage"></param>
    /// <param name="now"></param>
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

        EventShrink?.Invoke(pool);
        pool.DecreaseCapacity(step);
        _cooldowns[state.BufferSize] = now;
    }

    /// <summary>
    /// Adjusts the rent and return counters and decides whether to trigger a resize evaluation.
    /// </summary>
    /// <param name="poolSize"></param>
    /// <param name="isRent"></param>
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
    /// <param name="size"></param>
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
