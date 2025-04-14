using Notio.Common.Caching;
using Notio.Shared.Memory.Pools;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Notio.Shared.Memory.PoolTypes;

/// <summary>
/// A type-specific adapter for the object pool that eliminates the need for runtime type checking.
/// </summary>
/// <typeparam name="T">The type of objects managed by this pool.</typeparam>
public sealed class TypedObjectPool<T> where T : IPoolable, new()
{
    #region Fields

    private readonly ObjectPool _parentPool;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedObjectPool{T}"/> class.
    /// </summary>
    /// <param name="parentPool">The parent object pool.</param>
    internal TypedObjectPool(ObjectPool parentPool)
    {
        _parentPool = parentPool;
    }

    #endregion

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
    /// <returns>The Number of objects removed.</returns>
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
    /// <param name="count">The Number of objects to preallocate.</param>
    /// <returns>The Number of objects successfully preallocated.</returns>
    public int Prealloc(int count) => _parentPool.Prealloc<T>(count);

    /// <summary>
    /// Gets multiple objects from the pool.
    /// </summary>
    /// <param name="count">The Number of objects to get.</param>
    /// <returns>A list containing the requested objects.</returns>
    public List<T> GetMultiple(int count) => _parentPool.GetMultiple<T>(count);

    /// <summary>
    /// Gets information about this type's pool.
    /// </summary>
    /// <returns>A dictionary containing pool statistics for this type.</returns>
    public Dictionary<string, object> GetInfo() => _parentPool.GetTypeInfo<T>();

    /// <summary>
    /// Returns multiple objects to the pool.
    /// </summary>
    /// <param name="objects">The objects to return.</param>
    /// <returns>The Number of objects successfully returned to the pool.</returns>
    public int ReturnMultiple(IEnumerable<T> objects) => _parentPool.ReturnMultiple(objects);

    /// <summary>
    /// Sets the maximum capacity for this type's pool.
    /// </summary>
    /// <param name="maxCapacity">The maximum capacity.</param>
    public void SetMaxCapacity(int maxCapacity) => _parentPool.SetMaxCapacity<T>(maxCapacity);

    #endregion
}
