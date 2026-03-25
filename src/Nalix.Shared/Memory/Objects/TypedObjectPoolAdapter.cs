// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Shared;
using Nalix.Shared.Memory.Pools;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Objects;

/// <summary>
/// A type-specific adapter for the object pool that eliminates the need for runtime type checking.
/// </summary>
/// <typeparam name="T">The type of objects managed by this pool.</typeparam>
public sealed class TypedObjectPoolAdapter<T> where T : IPoolable, new()
{
    #region Fields

    private readonly ObjectPool _pool;
    private readonly ObjectPoolManager _manager;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedObjectPoolAdapter{T}"/> class.
    /// </summary>
    /// <param name="pool">The object pool.</param>
    /// <param name="manager">The object pool manager.</param>
    internal TypedObjectPoolAdapter(ObjectPool pool, ObjectPoolManager manager)
    {
        _pool = pool;
        _manager = manager;
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets an object from the pool.
    /// </summary>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public T Get()
    {
        _ = Interlocked.Increment(ref _manager._totalGetOperations);
        return _pool.Get<T>();
    }

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="obj">The object to return.</param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public void Return(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj, nameof(obj));

        _ = Interlocked.Increment(ref _manager._totalReturnOperations);
        _pool.Return(obj);
    }

    /// <summary>
    /// Gets multiple objects from the pool.
    /// </summary>
    /// <param name="count">The ProtocolType of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public List<T> GetMultiple(int count)
    {
        _ = Interlocked.Add(ref _manager._totalGetOperations, count);
        return _pool.GetMultiple<T>(count);
    }

    /// <summary>
    /// Trims this type's pool to a target size.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <returns>The ProtocolType of objects removed.</returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
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

        return _pool.Trim(percentage);
    }

    /// <summary>
    /// Returns multiple objects to the pool.
    /// </summary>
    /// <param name="objects">The objects to return.</param>
    /// <returns>The ProtocolType of objects successfully returned to the pool.</returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public int ReturnMultiple(IEnumerable<T> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);

        int count = _pool.ReturnMultiple(objects);
        _ = Interlocked.Add(ref _manager._totalReturnOperations, count);
        return count;
    }

    /// <summary>
    /// Clears this type's pool.
    /// </summary>
    /// <returns>The ProtocolType of objects removed.</returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public int Clear() => _pool.ClearType<T>();

    /// <summary>
    /// Preallocates objects in the pool.
    /// </summary>
    /// <param name="count">The ProtocolType of objects to preallocate.</param>
    /// <returns>The ProtocolType of objects successfully preallocated.</returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public int Prealloc(int count) => _pool.Prealloc<T>(count);

    /// <summary>
    /// Gets information about this type's pool.
    /// </summary>
    /// <returns>A dictionary containing pool statistics for this type.</returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object> GetInfo() => _pool.GetTypeInfo<T>();

    /// <summary>
    /// Sets the maximum capacity for this type's pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity.</param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public void SetMaxCapacity(int maxCapacity) => _pool.SetMaxCapacity<T>(maxCapacity);

    #endregion Public Methods
}
