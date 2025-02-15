using System.Collections.Generic;

namespace Notio.Shared.Memory.Cache;

/// <summary>
/// Represents a Least Recently Used (LRU) cache with a specified capacity.
/// </summary>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cache value.</typeparam>
public class LruCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private class CacheItem
    {
        /// <summary>
        /// Gets or sets the key of the cache item.
        /// </summary>
        public TKey? Key { get; set; }

        /// <summary>
        /// Gets or sets the value of the cache item.
        /// </summary>
        public TValue? Value { get; set; }
    }

    private readonly int _capacity = capacity;
    private readonly LinkedList<CacheItem> _usageOrder = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap = [];

    /// <summary>
    /// Adds an item to the cache.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <param name="value">The value of the item.</param>
    public void Add(TKey key, TValue value)
    {
        if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
        {
            // Remove the old node, update the value, and move it to the front
            _usageOrder.Remove(node);
            node.Value.Value = value;
        }
        else
        {
            // If the cache is full, evict the least recently used item
            if (_cacheMap.Count >= _capacity)
            {
                var lastNode = _usageOrder.Last;
                if (lastNode != null)
                {
                    _usageOrder.RemoveLast();
                    if (lastNode.Value.Key != null)
                        _cacheMap.Remove(lastNode.Value.Key);
                }
            }

            // Add the new item to the front of the list and map
            LinkedListNode<CacheItem> newNode = new(new CacheItem { Key = key, Value = value });
            _usageOrder.AddFirst(newNode);
            _cacheMap[key] = newNode;
        }
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the item to get.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the cache.</exception>
    public TValue GetValue(TKey key)
    {
        if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
        {
            // Move the node to the front to mark it as most recently used
            _usageOrder.Remove(node);
            _usageOrder.AddFirst(node);

            if (node.Value.Value == null)
                throw new KeyNotFoundException("The key was not found in the cache.");

            return node.Value.Value;
        }

        throw new KeyNotFoundException("The key was not found in the cache.");
    }

    /// <summary>
    /// Tries to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the item to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, null.</param>
    /// <returns>true if the key was found in the cache; otherwise, false.</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_cacheMap.TryGetValue(key, out LinkedListNode<CacheItem>? node))
        {
            _usageOrder.Remove(node);
            _usageOrder.AddFirst(node);
            value = node.Value.Value!;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        _cacheMap.Clear();
        _usageOrder.Clear();
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="LruCache{TKey, TValue}"/> class.
    /// </summary>
    ~LruCache()
    {
        Clear();
    }
}
