// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Framework.Memory.Pools;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Memory.Objects;

/// <summary>
/// A type-specific adapter for the object pool that eliminates the need for runtime type checking.
/// </summary>
/// <typeparam name="T">The type of objects managed by this pool.</typeparam>
public sealed class TypedObjectPool<T> where T : IPoolable, new()
{
    #region Fields

    private readonly ObjectPool _parentPool;
    private readonly ObjectPoolManager? _manager;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedObjectPool{T}"/> class.
    /// </summary>
    /// <param name="parentPool">The parent object pool.</param>
    /// <param name="manager">The optional object pool manager for statistics tracking.</param>
    internal TypedObjectPool(ObjectPool parentPool, ObjectPoolManager? manager = null)
    {
        _parentPool = parentPool;
        _manager = manager;
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets an object from the pool.
    /// </summary>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get()
    {
        if (_manager == null)
        {
            return _parentPool.Get<T>();
        }

        return _manager.Get<T>();
    }

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="obj">The object to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T obj)
    {
        if (_manager == null)
        {
            _parentPool.Return(obj);
            return;
        }

        _manager.Return(obj);
    }

    /// <summary>
    /// Gets multiple objects from the pool.
    /// </summary>
    /// <param name="count">The number of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<T> GetMultiple(int count)
    {
        if (_manager == null)
        {
            return _parentPool.GetMultiple<T>(count);
        }

        // ObjectPoolManager doesn't have GetMultiple, so we simulate it
        List<T> result = new(count);
        for (int i = 0; i < count; i++)
        {
            result.Add(_manager.Get<T>());
        }
        return result;
    }

    /// <summary>
    /// Returns multiple objects to the pool.
    /// </summary>
    /// <param name="objects">The objects to return.</param>
    /// <returns>The number of objects successfully returned to the pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReturnMultiple(IEnumerable<T> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);

        if (_manager == null)
        {
            return _parentPool.ReturnMultiple(objects);
        }

        int count = 0;
        foreach (T obj in objects)
        {
            _manager.Return(obj);
            count++;
        }
        return count;
    }

    /// <summary>
    /// Clears this type's pool.
    /// </summary>
    /// <returns>The number of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Clear() => _manager?.ClearPool<T>() ?? _parentPool.ClearType<T>();

    /// <summary>
    /// Preallocates objects in the pool.
    /// </summary>
    /// <param name="count">The number of objects to preallocate.</param>
    /// <returns>The number of objects successfully preallocated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Prealloc(int count) => _manager?.Prealloc<T>(count) ?? _parentPool.Prealloc<T>(count);

    /// <summary>
    /// Gets information about this type's pool.
    /// </summary>
    /// <returns>A dictionary containing pool statistics for this type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object> GetInfo() => _manager?.GetTypeInfo<T>() ?? _parentPool.GetTypeInfo<T>();

    /// <summary>
    /// Sets the maximum capacity for this type's pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMaxCapacity(int maxCapacity)
    {
        if (_manager != null)
        {
            _ = _manager.SetMaxCapacity<T>(maxCapacity);
        }
        else
        {
            _ = _parentPool.SetMaxCapacity<T>(maxCapacity);
        }
    }

    /// <summary>
    /// Trims this type's pool to a target size.
    /// </summary>
    /// <param name="percentage">The percentage of the maximum capacity to keep (0-100).</param>
    /// <returns>The number of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Trim(int percentage = 50) => _parentPool.Trim(percentage);

    #endregion Public Methods
}


