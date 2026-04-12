// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Nalix.Common.Abstractions;
using Nalix.Framework.Memory.Internal.PoolTypes;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Framework.Memory.Pools;

/// <summary>
/// A thread-safe pool that stores and reuses <see cref="IPoolable"/> instances by type.
/// Objects are reset before being returned to the pool so callers always receive a
/// clean instance on the next rent.
/// </summary>
/// <remarks>
/// Each pooled type gets its own internal bucket and capacity limit. The pool is
/// intentionally simple: rent fast, reset on return, and discard when full.
/// </remarks>
/// <param name="defaultMaxItemsPerType">The default maximum number of items to keep per pooled type.</param>
public sealed class ObjectPool(int defaultMaxItemsPerType)
{
    #region Constants

    /// <summary>
    /// Standard maximum pool size used when the caller does not provide a positive limit.
    /// </summary>
    public const int DefaultMaxSize = 1024;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Type-specific storage for pooled objects.
    /// Each concrete type gets its own bucket so instances never cross type boundaries.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, TypePool> _typePools = new();

    /// <summary>
    /// Statistics tracking for diagnostics and capacity tuning.
    /// </summary>
    private long _totalCreated;

    private long _totalReturned;
    private long _totalRented;
    private readonly System.Diagnostics.Stopwatch _uptime = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// Configuration for the default per-type pool capacity.
    /// </summary>
    private readonly int _defaultMaxItemsPerType = defaultMaxItemsPerType > 0 ? defaultMaxItemsPerType : DefaultMaxSize;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the singleton instance of the object pool.
    /// </summary>
    public static ObjectPool Default { get; } = new();

    /// <summary>
    /// Gets the total number of objects created across all pooled types.
    /// </summary>
    public long TotalCreatedCount
    {
        get
        {
            System.Threading.Thread.MemoryBarrier();
            return System.Threading.Volatile.Read(ref _totalCreated);
        }
    }

    /// <summary>
    /// Gets the total number of objects currently available across all pools.
    /// </summary>
    public int TotalAvailableCount
    {
        get
        {
            int count = 0;
            foreach (TypePool pool in _typePools.Values)
            {
                count += pool.AvailableCount;
            }
            return count;
        }
    }

    /// <summary>
    /// Gets the number of distinct object types currently being pooled.
    /// </summary>
    public int TypeCount => _typePools.Count;

    /// <summary>
    /// Gets the total number of objects returned to the pool.
    /// </summary>
    public long TotalReturnedCount => System.Threading.Interlocked.Read(ref _totalReturned);

    /// <summary>
    /// Gets the total number of objects rented from the pool.
    /// </summary>
    public long TotalRentedCount => System.Threading.Interlocked.Read(ref _totalRented);

    /// <summary>
    /// Gets the pool uptime in milliseconds.
    /// </summary>
    public long UptimeMs => _uptime.ElapsedMilliseconds;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPool"/> class with default settings.
    /// </summary>
    public ObjectPool() : this(DefaultMaxSize)
    {
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets an instance of <typeparamref name="T"/>, creating a new one when the pool is empty.
    /// </summary>
    /// <typeparam name="T">The type of object to get from the pool.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public T Get<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);

        // Resolve the bucket for this type once per rent call.
        TypePool typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        // Rent from the bucket when possible; otherwise create a fresh instance.
        if (typePool.TryPop(out IPoolable? obj) && obj != null)
        {
            _ = System.Threading.Interlocked.Increment(ref _totalRented);
            return (T)obj;
        }

        // Pool miss: create a new instance and account for it as a fresh allocation.
        T newObj = new();

        _ = System.Threading.Interlocked.Increment(ref _totalCreated);
        _ = System.Threading.Interlocked.Increment(ref _totalRented);

        return newObj;
    }

    /// <summary>
    /// Returns an instance of <typeparamref name="T"/> to the pool for future reuse.
    /// </summary>
    /// <typeparam name="T">The type of object to return to the pool.</typeparam>
    /// <param name="obj">The object to return to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return<T>(T obj) where T : IPoolable, new()
    {
        if (EqualityComparer<T>.Default.Equals(obj, default))
        {
            throw new ArgumentNullException(nameof(obj));
        }

        Type type = typeof(T);

        // Reset first so the next renter always sees a clean object.
        obj.ResetForPool();

        // Return to the same bucket used for rent.
        TypePool typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        // If the bucket is full we simply drop the instance and let GC reclaim it.
        if (typePool.TryPush(obj))
        {
            _ = System.Threading.Interlocked.Increment(ref _totalReturned);
            return;
        }

        // Capacity reached: discard instead of growing without bound.
    }

    /// <summary>
    /// Preallocates and stores multiple new instances of <typeparamref name="T"/> in the pool.
    /// </summary>
    /// <typeparam name="T">The type of objects to preallocate.</typeparam>
    /// <param name="count">The number of instances to preallocate.</param>
    /// <returns>The number of instances successfully added to the pool.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int Prealloc<T>(int count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            return 0;
        }

        Type type = typeof(T);
        TypePool typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        int created = 0;
        for (int i = 0; i < count; i++)
        {
            // Preallocation stops as soon as the bucket reports that it is full.
            T obj = new();
            if (typePool.TryPush(obj))
            {
                created++;
                _ = System.Threading.Interlocked.Increment(ref _totalCreated);
                _ = System.Threading.Interlocked.Increment(ref _totalReturned);
            }
            else
            {
                // Stop once capacity is reached so preallocation does not overshoot the limit.
                break;
            }
        }

        return created;
    }

    /// <summary>
    /// Sets the maximum capacity for a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to configure.</typeparam>
    /// <param name="maxCapacity">The maximum capacity for the type's pool.</param>
    /// <returns>True if the capacity was set, false otherwise.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool SetMaxCapacity<T>(int maxCapacity) where T : IPoolable
    {
        if (maxCapacity < 0)
        {
            return false;
        }

        Type type = typeof(T);
        if (_typePools.TryGetValue(type, out TypePool? typePool))
        {
            typePool.SetMaxCapacity(maxCapacity);
            return true;
        }

        // Create a new pool with the specified capacity
        _typePools[type] = new TypePool(maxCapacity);
        return true;
    }

    /// <summary>
    /// Gets information about a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to get information about.</typeparam>
    /// <returns>A dictionary containing pool statistics for the type.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object> GetTypeInfo<T>() where T : IPoolable
    {
        Type type = typeof(T);
        return _typePools.TryGetValue(type, out TypePool? typePool)
            ? CREATE_TYPE_INFO(type.Name, typePool.AvailableCount, typePool.MaxCapacity, true)
            : CREATE_TYPE_INFO(type.Name, 0, _defaultMaxItemsPerType, false);
    }

    /// <summary>
    /// Gets statistics about the pool's usage.
    /// </summary>
    /// <returns>A dictionary containing statistics about the pool.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object> GetStatistics()
    {
        return new Dictionary<string, object>(8, StringComparer.Ordinal)
        {
            ["TotalCreatedCount"] = this.TotalCreatedCount,
            ["TotalAvailableCount"] = this.TotalAvailableCount,
            ["TypeCount"] = this.TypeCount,
            ["TotalRentedCount"] = this.TotalRentedCount,
            ["TotalReturnedCount"] = this.TotalReturnedCount,
            ["ActiveRentals"] = this.TotalRentedCount - this.TotalReturnedCount,
            ["UptimeMs"] = this.UptimeMs,
            ["DefaultMaxItemsPerType"] = _defaultMaxItemsPerType
        };
    }

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    /// <returns>The total ProtocolType of objects removed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int Clear()
    {
        int removedCount = 0;
        foreach (TypePool pool in _typePools.Values)
        {
            removedCount += pool.Clear();
        }

        return removedCount;
    }

    /// <summary>
    /// Clears a specific type's pool.
    /// </summary>
    /// <typeparam name="T">The type to clear from the pool.</typeparam>
    /// <returns>The ProtocolType of objects removed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int ClearType<T>() where T : IPoolable
    {
        Type type = typeof(T);
        if (_typePools.TryGetValue(type, out TypePool? typePool))
        {
            int removedCount = typePool.Clear();
            return removedCount;
        }

        return 0;
    }

    /// <summary>
    /// Trims all type pools to their target sizes.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to trim to (0-100).</param>
    /// <returns>The total ProtocolType of objects removed.</returns>
    public int Trim(int percentage = 50)
    {
        if (percentage < 0)
        {
            percentage = 0;
        }

        if (percentage > 100)
        {
            percentage = 100;
        }

        int removedCount = 0;
        foreach (TypePool pool in _typePools.Values)
        {
            removedCount += pool.Trim(percentage);
        }

        return removedCount;
    }

    /// <summary>
    /// Resets the statistics of the pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetStatistics()
    {
        _ = System.Threading.Interlocked.Exchange(ref _totalCreated, 0);
        _ = System.Threading.Interlocked.Exchange(ref _totalRented, 0);
        _ = System.Threading.Interlocked.Exchange(ref _totalReturned, 0);
        _uptime.Restart();

    }

    /// <summary>
    /// Gets information about all type pools.
    /// </summary>
    /// <returns>A list of dictionaries containing information about each type pool.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public List<
        Dictionary<string, object>> GetAllTypeInfo()
    {
        List<
            Dictionary<string, object>> result = new(_typePools.Count);

        foreach (KeyValuePair<Type, TypePool> kvp in _typePools)
        {
            TypePool typePool = kvp.Value;
            result.Add(CREATE_TYPE_INFO(kvp.Key.Name, typePool.AvailableCount, typePool.MaxCapacity, true));
        }

        return result;
    }

    /// <summary>
    /// Batch returns multiple objects to the pool.
    /// </summary>
    /// <typeparam name="T">The type of objects to return.</typeparam>
    /// <param name="objects">The objects to return to the pool.</param>
    /// <returns>The ProtocolType of objects successfully returned to the pool.</returns>
    /// <exception cref="ArgumentNullException">Thrown when objects is null.</exception>
    public int ReturnMultiple<T>(IEnumerable<T> objects) where T : IPoolable, new()
    {
        ArgumentNullException.ThrowIfNull(objects);

        int returnedCount = 0;
        Type type = typeof(T);
        TypePool typePool = _typePools.GetOrAdd(type, _ => new TypePool(_defaultMaxItemsPerType));

        foreach (T obj in objects)
        {
            if (EqualityComparer<T>.Default.Equals(obj, default))
            {
                continue;
            }

            obj.ResetForPool();

            if (typePool.TryPush(obj))
            {
                returnedCount++;
                _ = System.Threading.Interlocked.Increment(ref _totalReturned);
            }
        }

        return returnedCount;
    }

    /// <summary>
    /// Gets multiple objects from the pool at once.
    /// </summary>
    /// <typeparam name="T">The type of objects to get.</typeparam>
    /// <param name="count">The ProtocolType of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    /// <exception cref="ArgumentException">Thrown when count is less than or equal to zero.</exception>
    public List<T> GetMultiple<T>(int count) where T : IPoolable, new()
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        List<T> result = new(count);
        _ = typeof(T);

        for (int i = 0; i < count; i++)
        {
            result.Add(this.Get<T>());
        }

        return result;
    }

    /// <summary>
    /// Creates a new type-specific pool adapter for more efficient operations with a specific type.
    /// </summary>
    /// <typeparam name="T">The type of objects to manage in the pool.</typeparam>
    /// <returns>A type-specific pool adapter.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public TypedObjectPool<T> CreateTypedPool<T>() where T : IPoolable, new() => new(this);

    internal Dictionary<string, object> GetTypeInfoByType(Type type)
    {
        return _typePools.TryGetValue(type, out TypePool? typePool)
            ? CREATE_TYPE_INFO(type.Name, typePool.AvailableCount, typePool.MaxCapacity, true)
            : CREATE_TYPE_INFO(type.Name, 0, _defaultMaxItemsPerType, false);
    }

    #endregion Public Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, object> CREATE_TYPE_INFO(string typeName, int availableCount, int maxCapacity, bool isActive)
    {
        return new Dictionary<string, object>(4, StringComparer.Ordinal)
        {
            ["TypeName"] = typeName,
            ["AvailableCount"] = availableCount,
            ["MaxCapacity"] = maxCapacity,
            ["IsActive"] = isActive
        };
    }
}
