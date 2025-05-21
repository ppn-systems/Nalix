// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Shared.Memory.PoolTypes;

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Get() => _parentPool.Get<T>();

    /// <summary>
    /// Clears this type's pool.
    /// </summary>
    /// <returns>The ProtocolType of objects removed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int32 Clear() => _parentPool.ClearType<T>();

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="obj">The object to return.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Return(T obj) => _parentPool.Return(obj);

    /// <summary>
    /// Preallocates objects in the pool.
    /// </summary>
    /// <param name="count">The ProtocolType of objects to preallocate.</param>
    /// <returns>The ProtocolType of objects successfully preallocated.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int32 Prealloc(System.Int32 count) => _parentPool.Prealloc<T>(count);

    /// <summary>
    /// Gets multiple objects from the pool.
    /// </summary>
    /// <param name="count">The ProtocolType of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> GetMultiple(System.Int32 count) => _parentPool.GetMultiple<T>(count);

    /// <summary>
    /// Gets information about this type's pool.
    /// </summary>
    /// <returns>A dictionary containing pool statistics for this type.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetInfo() => _parentPool.GetTypeInfo<T>();

    /// <summary>
    /// Returns multiple objects to the pool.
    /// </summary>
    /// <param name="objects">The objects to return.</param>
    /// <returns>The ProtocolType of objects successfully returned to the pool.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Int32 ReturnMultiple(System.Collections.Generic.IEnumerable<T> objects) => _parentPool.ReturnMultiple(objects);

    /// <summary>
    /// Sets the maximum capacity for this type's pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetMaxCapacity(System.Int32 maxCapacity) => _parentPool.SetMaxCapacity<T>(maxCapacity);

    #endregion Public Methods
}
