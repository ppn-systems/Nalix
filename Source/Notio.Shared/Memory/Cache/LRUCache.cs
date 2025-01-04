using Notio.Shared.Memory.Extensions;
using System.Collections.Generic;

namespace Notio.Shared.Memory.Cache;

/// <summary>
/// Represents a Least Recently Used (LRU) cache with a specified capacity.
/// </summary>
/// <param name="capacity">The maximum capacity of the cache.</param>
public sealed class LRUCache(int capacity)
{
    private class CacheItem
    {
        /// <summary>
        /// Gets or sets the key of the cache item.
        /// </summary>
        public byte[]? Key { get; set; }

        /// <summary>
        /// Gets or sets the value of the cache item.
        /// </summary>
        public byte[]? Value { get; set; }
    }

    private readonly int _capacity = capacity;
    private readonly Dictionary<byte[], LinkedListNode<CacheItem>> _cacheMap = new(new ByteArrayComparer());
    private readonly LinkedList<CacheItem> _lruList = new();

    /// <summary>
    /// Adds an item to the cache.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <param name="value">The value of the item.</param>
    public void Add(byte[] key, byte[] value)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            node.Value.Value = value;
        }
        else
        {
            if (_cacheMap.Count >= _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _lruList.RemoveLast();

                    if (lastNode.Value.Key != null)
                        _cacheMap.Remove(lastNode.Value.Key);
                }
            }

            var newNode = new LinkedListNode<CacheItem>(new CacheItem { Key = key, Value = value });
            _lruList.AddFirst(newNode);
            _cacheMap[key] = newNode;
        }
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the item to get.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the cache.</exception>
    public byte[] GetValue(byte[] key)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            // Move the node to the front to mark it as most recently used
            _lruList.Remove(node);
            _lruList.AddFirst(node);

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
    public bool TryGetValue(byte[] key, out byte[]? value)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            value = node.Value.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        _cacheMap.Clear();
        _lruList.Clear();
    }
}