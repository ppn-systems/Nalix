// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Memory.Pools;

/// <summary>
/// Provides a pool of reusable <see cref="List{T}"/> instances, similar to ArrayPool,
/// optimized for high-performance real-time server scenarios.
/// </summary>
/// <typeparam name="T">The type of elements in the lists.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ListPool{T}"/> class.
/// </remarks>
/// <param name="maxPoolSize">The maximum TransportProtocol of lists to keep in the pool.</param>
/// <param name="initialCapacity">The initial capacity of new lists.</param>
public sealed class ListPool<T>(Int32 maxPoolSize, Int32 initialCapacity)
{
    #region Constants

    // Configuration constants
    private const Int32 DefaultMaxPoolSize = 1024;

    private const Int32 DefaultInitialCapacity = 16;
    private const Int32 MaxInitialCapacity = 8192;

    #endregion Constants

    #region Fields

    // Thread-safe storage for pooled lists
    private readonly ConcurrentBag<List<T>> _listBag = [];

    // Statistics tracking
    private Int64 _rented;

    private Int64 _returned;
    private Int64 _created;
    private Int64 _trimmed;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    private readonly Int32 _maxPoolSize = maxPoolSize > 0 ? maxPoolSize : DefaultMaxPoolSize;
    private readonly Int32 _initialCapacity = ValidateCapacity(initialCapacity);

    #endregion Fields

    #region Properties

    /// <summary>
    /// Event for trace information.
    /// </summary>
    public event Action<String>? TraceOccurred;

    /// <summary>
    /// Gets the singleton instance of the <see cref="ListPool{T}"/>.
    /// </summary>
    public static ListPool<T> Instance { get; } = new();

    /// <summary>
    /// Gets the TransportProtocol of lists currently available in the pool.
    /// </summary>
    public Int32 AvailableCount => _listBag.Count;

    /// <summary>
    /// Gets the total TransportProtocol of lists created by this pool.
    /// </summary>
    public Int64 CreatedCount => Interlocked.Read(ref _created);

    /// <summary>
    /// Gets the TransportProtocol of lists currently rented from the pool.
    /// </summary>
    public Int64 RentedCount => Interlocked.Read(ref _rented) - Interlocked.Read(ref _returned);

    /// <summary>
    /// Gets the total TransportProtocol of rent operations performed.
    /// </summary>
    public Int64 TotalRentOperations => Interlocked.Read(ref _rented);

    /// <summary>
    /// Gets the total TransportProtocol of return operations performed.
    /// </summary>
    public Int64 TotalReturnOperations => Interlocked.Read(ref _returned);

    /// <summary>
    /// Gets the TransportProtocol of lists that have been trimmed from the pool.
    /// </summary>
    public Int64 TrimmedCount => Interlocked.Read(ref _trimmed);

    /// <summary>
    /// Gets the uptime of the pool in milliseconds.
    /// </summary>
    public Int64 UptimeMs => _uptime.ElapsedMilliseconds;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes the default instance of the <see cref="ListPool{T}"/>.
    /// </summary>
    private ListPool()
        : this(DefaultMaxPoolSize, DefaultInitialCapacity)
    {
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Rents a <see cref="List{T}"/> instance from the pool.
    /// </summary>
    /// <param name="minimumCapacity">Optional. The minimum capacity required for the list.</param>
    /// <returns>A <see cref="List{T}"/> instance that can be used and then returned to the pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<T> Rent(Int32 minimumCapacity = 0)
    {
        _ = Interlocked.Increment(ref _rented);

        // Try to get a list from the pool
        if (_listBag.TryTake(out List<T>? list))
        {
            // If a minimum capacity is specified, ensure the list meets it
            if (minimumCapacity > 0 && list.Capacity < minimumCapacity)
            {
                list.Capacity = minimumCapacity;
            }

            return list;
        }

        // Create a new list with appropriate capacity
        Int32 capacity = minimumCapacity > 0 ? Math.Max(minimumCapacity, _initialCapacity) : _initialCapacity;
        var newList = new List<T>(capacity);

        _ = Interlocked.Increment(ref _created);
        TraceOccurred?.Invoke($"Rent(): Created new List<{typeof(T).Name}> instance (Capacity={capacity}, TotalCreated={CreatedCount})");

        return newList;
    }

    /// <summary>
    /// Returns a <see cref="List{T}"/> to the pool.
    /// </summary>
    /// <param name="list">The list to return to the pool.</param>
    /// <param name="clearItems">Whether to clear the list before returning it. Standard is true.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(List<T> list, Boolean clearItems = true)
    {
        ArgumentNullException.ThrowIfNull(list);

        _ = Interlocked.Increment(ref _returned);

        // Dispose the list if requested
        if (clearItems)
        {
            list.Clear();
        }

        // Only add to the pool if we haven't reached the maximum size
        if (_listBag.Count < _maxPoolSize)
        {
            _listBag.Add(list);
        }
        else
        {
            // The list will be garbage collected since we're not storing it
            _ = Interlocked.Increment(ref _trimmed);
            TraceOccurred?.Invoke($"Return(): Pools size limit reached, discarding list (MaxSize={_maxPoolSize}, Trimmed={TrimmedCount})");
        }
    }

    /// <summary>
    /// Creates and initializes multiple lists in the pool.
    /// </summary>
    /// <param name="count">The TransportProtocol of lists to preallocate.</param>
    /// <param name="capacity">The capacity for each preallocated list.</param>
    public void Prealloc(Int32 count, Int32 capacity = 0)
    {
        if (count <= 0)
        {
            return;
        }

        Int32 listCapacity = capacity > 0 ? capacity : _initialCapacity;
        Int32 preallocationCount = Math.Min(count, _maxPoolSize - _listBag.Count);

        if (preallocationCount <= 0)
        {
            return;
        }

        for (Int32 i = 0; i < preallocationCount; i++)
        {
            var list = new List<T>(listCapacity);
            _listBag.Add(list);
            _ = Interlocked.Increment(ref _created);
        }

        TraceOccurred?.Invoke($"Prealloc(): Created {preallocationCount} List<{typeof(T).Name}> instances (Capacity={listCapacity})");
    }

    /// <summary>
    /// Trims the pool to a specified size.
    /// </summary>
    /// <param name="maximumSize">The maximum TransportProtocol of lists to keep in the pool.</param>
    /// <returns>The TransportProtocol of lists removed from the pool.</returns>
    public Int32 Trim(Int32 maximumSize = 0)
    {
        Int32 targetSize = maximumSize > 0 ? maximumSize : _maxPoolSize / 2;
        Int32 trimCount = 0;

        while (_listBag.Count > targetSize && _listBag.TryTake(out _))
        {
            trimCount++;
        }

        if (trimCount > 0)
        {
            _ = Interlocked.Add(ref _trimmed, trimCount);
            TraceOccurred?.Invoke($"Trim(): Removed {trimCount} List<{typeof(T).Name}> instances from pool");
        }

        return trimCount;
    }

    /// <summary>
    /// Clears all lists from the pool.
    /// </summary>
    /// <returns>The TransportProtocol of lists removed from the pool.</returns>
    public Int32 Clear()
    {
        Int32 count = _listBag.Count;

        while (_listBag.TryTake(out _)) { }

        _ = Interlocked.Add(ref _trimmed, count);
        TraceOccurred?.Invoke($"Dispose(): Removed all {count} List<{typeof(T).Name}> instances from pool");

        return count;
    }

    /// <summary>
    /// Gets statistics about the pool's usage.
    /// </summary>
    /// <returns>A dictionary containing statistics about the pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<String, Object> GetStatistics()
    {
        return new Dictionary<String, Object>
        {
            ["MaxPoolSize"] = _maxPoolSize,
            ["InitialCapacity"] = _initialCapacity,
            ["AvailableCount"] = AvailableCount,
            ["CreatedCount"] = CreatedCount,
            ["RentedCount"] = RentedCount,
            ["TotalRentOperations"] = TotalRentOperations,
            ["TotalReturnOperations"] = TotalReturnOperations,
            ["TrimmedCount"] = TrimmedCount,
            ["UptimeMs"] = UptimeMs
        };
    }

    /// <summary>
    /// Resets the statistics of the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        _ = Interlocked.Exchange(ref _rented, 0);
        _ = Interlocked.Exchange(ref _returned, 0);
        _ = Interlocked.Exchange(ref _created, 0);
        _ = Interlocked.Exchange(ref _trimmed, 0);
        _uptime.Restart();
    }

    #endregion Public Methods

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int32 ValidateCapacity(Int32 capacity)
    {
        if (capacity <= 0)
        {
            return DefaultInitialCapacity;
        }

        return capacity > MaxInitialCapacity ? MaxInitialCapacity : capacity;
    }

    #endregion Private Methods
}
