// Nalix.Network/Internal/Pooling/LocalPool.cs

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Network.Internal.Pooling;

/// <summary>
/// Represents a small, connection-scoped object pool optimized for fast, lock-free access.
///
/// <para>
/// This pool acts as a <b>local cache layer</b> on top of a global <see cref="ObjectPoolManager"/>.
/// It minimizes contention and allocations by keeping a fixed-size array of reusable objects
/// bound to a specific connection.
/// </para>
///
/// <para>
/// <b>Design characteristics:</b>
/// <list type="bullet">
/// <item><description>Fixed-size pool (8 slots) using a bitmask for tracking usage.</description></item>
/// <item><description>Lock-free acquisition using <see cref="Interlocked"/> operations.</description></item>
/// <item><description>Safe fallback to global pool when local pool is unavailable or destroyed.</description></item>
/// <item><description>No object-level awareness of pool ownership (pool is externally managed).</description></item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="T">
/// The pooled object type. Must implement <see cref="IPoolable"/> and provide a parameterless constructor.
/// </typeparam>
internal sealed class LocalPool<T> where T : class, IPoolable, new()
{
    /// <summary>
    /// The fixed number of slots in the local pool.
    /// </summary>
    private const int Size = 8;

    /// <summary>
    /// Reference to the global pool manager used as a fallback and source of objects.
    /// </summary>
    private readonly ObjectPoolManager _globalPool;

    /// <summary>
    /// Backing storage for pooled items. Null indicates the pool has not been initialized
    /// or has been destroyed.
    /// </summary>
    private T[]? _items;

    /// <summary>
    /// Bitmask representing slot usage:
    /// 1 = occupied (busy), 0 = available (free).
    /// </summary>
    private long _mask;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalPool{T}"/> class.
    /// </summary>
    /// <param name="globalPool">The global pool manager used for fallback operations.</param>
    public LocalPool(ObjectPoolManager globalPool) => _globalPool = globalPool;

    /// <summary>
    /// Attempts to acquire an object from the local pool.
    /// </summary>
    /// <param name="initialize">
    /// A delegate used to initialize each object during first-time pool creation.
    /// </param>
    /// <returns>
    /// An available pooled object if a free slot exists; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method is lock-free for the fast path and uses a bitmask to claim a slot atomically.
    /// If the pool has not been initialized yet, it will be lazily created.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? Acquire(System.Action<T> initialize)
    {
        this.EnsureInitialized(initialize);

        T[] items = _items!;
        for (int i = 0; i < Size; i++)
        {
            long bit = 1L << i;

            // Check if slot is free and attempt to claim it atomically
            if ((Interlocked.Read(ref _mask) & bit) == 0 &&
                (Interlocked.Or(ref _mask, bit) & bit) == 0)
            {
                return items[i];
            }
        }

        // All slots are busy
        return null;
    }

    /// <summary>
    /// Returns an object back to the pool.
    /// </summary>
    /// <param name="item">The object to return.</param>
    /// <remarks>
    /// If the local pool is still active and owns the object, it will be returned to its slot.
    /// Otherwise, the object is safely returned to the global pool.
    ///
    /// This guarantees that objects are never leaked, even after pool destruction.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        T[]? items = _items;

        if (items != null)
        {
            for (int i = 0; i < Size; i++)
            {
                if (ReferenceEquals(items[i], item))
                {
                    item.ResetForPool();

                    // Mark slot as free
                    _ = Interlocked.And(ref _mask, ~(1L << i));
                    return;
                }
            }
        }

        // Pool destroyed or foreign object → fallback to global pool
        item.ResetForPool();
        _globalPool.Return(item);
    }

    /// <summary>
    /// Destroys the local pool and releases its resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is typically called when the associated connection is disposed.
    /// </para>
    ///
    /// <para>
    /// Behavior:
    /// <list type="bullet">
    /// <item><description>Idle objects are immediately returned to the global pool.</description></item>
    /// <item><description>
    /// Busy objects are not forcibly reclaimed. Instead, they will automatically
    /// fall back to the global pool when <see cref="Return(T)"/> is called.
    /// </description></item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// After destruction, the local pool becomes inactive and all operations will
    /// transparently use the global pool.
    /// </para>
    /// </remarks>
    public void Destroy()
    {
        T[]? items = Interlocked.Exchange(ref _items, null);
        if (items == null)
        {
            return;
        }

        long mask = Interlocked.Read(ref _mask);

        for (int i = 0; i < Size; i++)
        {
            T? item = items[i];
            if (item == null)
            {
                continue;
            }

            bool isBusy = (mask & (1L << i)) != 0;

            if (!isBusy)
            {
                // Immediately return idle items to global pool
                item.ResetForPool();
                _globalPool.Return(item);
            }

            // Busy items will fallback to global pool upon Return()
        }

        ArrayPool<T>.Shared.Return(items, clearArray: true);
    }

    /// <summary>
    /// Ensures the local pool is initialized.
    /// </summary>
    /// <param name="initialize">
    /// A delegate used to initialize each pooled object during creation.
    /// </param>
    /// <remarks>
    /// Uses double-checked locking to avoid unnecessary synchronization.
    /// Objects are preallocated from the global pool and initialized once.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialized(System.Action<T> initialize)
    {
        if (_items != null)
        {
            return;
        }

        lock (this)
        {
            if (_items != null)
            {
                return;
            }

            T[] arr = ArrayPool<T>.Shared.Rent(Size);

            for (int i = 0; i < Size; i++)
            {
                arr[i] = _globalPool.Get<T>();
                initialize(arr[i]);
            }

            _items = arr;
        }
    }
}
