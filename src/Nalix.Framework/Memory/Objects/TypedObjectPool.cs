// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Framework.Memory.Pools;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
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

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedObjectPool{T}"/> class.
    /// </summary>
    /// <param name="parentPool">The parent object pool.</param>
    internal TypedObjectPool(ObjectPool parentPool) => _parentPool = parentPool;

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets an object from the pool.
    /// </summary>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get() => _parentPool.Get<T>();

    /// <summary>
    /// Clears this type's pool.
    /// </summary>
    /// <returns>The ProtocolType of objects removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Clear() => _parentPool.ClearType<T>();

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="obj">The object to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T obj) => _parentPool.Return(obj);

    /// <summary>
    /// Preallocates objects in the pool.
    /// </summary>
    /// <param name="count">The ProtocolType of objects to preallocate.</param>
    /// <returns>The ProtocolType of objects successfully preallocated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Prealloc(int count) => _parentPool.Prealloc<T>(count);

    /// <summary>
    /// Gets multiple objects from the pool.
    /// </summary>
    /// <param name="count">The ProtocolType of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<T> GetMultiple(int count) => _parentPool.GetMultiple<T>(count);

    /// <summary>
    /// Gets information about this type's pool.
    /// </summary>
    /// <returns>A dictionary containing pool statistics for this type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object> GetInfo() => _parentPool.GetTypeInfo<T>();

    /// <summary>
    /// Returns multiple objects to the pool.
    /// </summary>
    /// <param name="objects">The objects to return.</param>
    /// <returns>The ProtocolType of objects successfully returned to the pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReturnMultiple(IEnumerable<T> objects) => _parentPool.ReturnMultiple(objects);

    /// <summary>
    /// Sets the maximum capacity for this type's pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMaxCapacity(int maxCapacity) => _parentPool.SetMaxCapacity<T>(maxCapacity);

    #endregion Public Methods
}
