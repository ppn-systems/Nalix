// Copyright (c)2025 - 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Memory.Objects;

/// <summary>
/// Represents a thread-safe key-value map backed by <see cref="ConcurrentDictionary{TKey, TValue}"/>,
/// integrated with an object pool to reduce allocations and improve performance in high-concurrency scenarios.
/// </summary>
/// <typeparam name="TKey">The type of keys in the map.</typeparam>
/// <typeparam name="TValue">The type of values in the map.</typeparam>
/// <remarks>
/// This type is designed for server environments where dictionaries are frequently created and accessed
/// concurrently across multiple threads.
///
/// Instead of allocating new instances, use <see cref="Rent"/> and <see cref="Return"/> to reuse objects
/// and minimize garbage collection pressure.
///
/// ⚠️ Notes:
/// <list type="bullet">
/// <item>
/// <description>All operations are thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.</description>
/// </item>
/// <item>
/// <description>The instance must not be used after calling <see cref="Return"/>.</description>
/// </item>
/// <item>
/// <description>
/// Enumeration represents a moment-in-time snapshot and may not reflect subsequent updates.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed class ObjectMap<TKey, TValue> : IObjectMap<TKey, TValue>
#if NET7_0_OR_GREATER
    where TKey : notnull
#else
    where TKey : class
#endif
{
    #region Fields

    /// <summary>
    /// Gets the shared object pool for <see cref="ObjectMap{TKey, TValue}"/>.
    /// </summary>
    private static readonly ObjectPoolManager s_objectMapPool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    /// <summary>
    /// The underlying thread-safe dictionary.
    /// </summary>
    private readonly ConcurrentDictionary<TKey, TValue> _dict = new();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the key does not exist during a get operation.
    /// </exception>
    public TValue this[TKey key]
    {
        get => _dict[key];
        set => _dict[key] = value;
    }

    /// <summary>
    /// Gets a collection containing the keys in the map.
    /// </summary>
    public ICollection<TKey> Keys => _dict.Keys;

    /// <summary>
    /// Gets a collection containing the values in the map.
    /// </summary>
    public ICollection<TValue> Values => _dict.Values;

    /// <summary>
    /// Gets the number of elements contained in the map.
    /// </summary>
    public int Count => _dict.Count;

    /// <summary>
    /// Gets a value indicating whether the map is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    #endregion Properties

    #region APIs

    /// <summary>
    /// Attempts to add the specified key and value to the map.
    /// </summary>
    /// <remarks>
    /// If the key already exists, the operation is ignored.
    /// </remarks>
    public void Add(TKey key, TValue value) => _dict.TryAdd(key, value);

    /// <summary>
    /// Determines whether the map contains the specified key.
    /// </summary>
    public bool ContainsKey(TKey key) => _dict.ContainsKey(key);

    /// <summary>
    /// Attempts to remove the value with the specified key.
    /// </summary>
    /// <returns><c>true</c> if the element was removed; otherwise, <c>false</c>.</returns>
    public bool Remove(TKey key) => _dict.TryRemove(key, out _);

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _dict.TryGetValue(key, out value);

    /// <summary>
    /// Attempts to add the specified key/value pair to the map.
    /// </summary>
    public void Add(KeyValuePair<TKey, TValue> item) => _dict.TryAdd(item.Key, item.Value);

    /// <summary>
    /// Removes all elements from the map.
    /// </summary>
    public void Clear() => _dict.Clear();

    /// <summary>
    /// Determines whether the map contains a specific key/value pair.
    /// </summary>
    public bool Contains(KeyValuePair<TKey, TValue> item) => _dict.TryGetValue(item.Key, out TValue? val) && EqualityComparer<TValue>.Default.Equals(val, item.Value);

    /// <summary>
    /// Copies the elements of the map to an array, starting at the specified index.
    /// </summary>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
        ((IDictionary<TKey, TValue>)_dict).CopyTo(array, arrayIndex);

    /// <summary>
    /// Attempts to remove a specific key/value pair from the map.
    /// </summary>
    public bool Remove(KeyValuePair<TKey, TValue> item) => _dict.TryRemove(item.Key, out TValue? val) && EqualityComparer<TValue>.Default.Equals(val, item.Value);

    /// <summary>
    /// Returns an enumerator that iterates through the map.
    /// </summary>
    /// <remarks>
    /// The enumerator represents a snapshot of the collection at a point in time.
    /// </remarks>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dict.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    /// Resets the internal state before returning the instance to the pool.
    /// </summary>
    /// <remarks>
    /// Clears all entries while preserving internal capacity for reuse efficiency.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForPool() => _dict.Clear();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return() => s_objectMapPool.Return(this);

    /// <summary>
    /// Retrieves an instance from the object pool.
    /// </summary>
    /// <returns>A reusable <see cref="ObjectMap{TKey, TValue}"/> instance.</returns>
    /// <remarks>
    /// The returned instance is logically empty but may retain internal capacity.
    /// Always call <see cref="Return"/> after use.
    /// </remarks>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
    public static ObjectMap<TKey, TValue> Rent() => s_objectMapPool.Get<ObjectMap<TKey, TValue>>();

    #endregion APIs
}
