using System;
using System.Collections.Generic;

namespace Notio.Shared.Memory.Cache;

/// <summary>
/// Initializes a cache with a specified capacity.
/// </summary>
public sealed class BinaryCache(int capacity)
{
    private readonly int _capacity = capacity;
    private readonly LinkedList<ReadOnlyMemory<byte>> _usageOrder = new();
    private readonly Dictionary<ReadOnlyMemory<byte>, LinkedListNode<ReadOnlyMemory<byte>>> _cacheMap = [];

    /// <summary>
    /// Adds an item to the cache.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <param name="value">The value of the item.</param>
    public void Add(ReadOnlySpan<byte> key, ReadOnlyMemory<byte> value)
    {
        ReadOnlyMemory<byte> memoryKey = key.ToArray();

        if (_cacheMap.TryGetValue(memoryKey, out var node))
        {
            node.Value = value;
            _usageOrder.Remove(node);
            _usageOrder.AddFirst(node);
        }
        else
        {
            if (_cacheMap.Count >= _capacity)
                EvictLeastUsedItem();

            var newNode = new LinkedListNode<ReadOnlyMemory<byte>>(value);
            _usageOrder.AddFirst(newNode);
            _cacheMap[memoryKey] = newNode;
        }
    }

    /// <summary>
    /// Retrieves the value from the cache by the given key.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <returns>The value of the item if found.</returns>
    /// <exception cref="KeyNotFoundException">Throws an exception if the key is not found.</exception>
    public ReadOnlyMemory<byte> GetValue(ReadOnlySpan<byte> key)
    {
        ReadOnlyMemory<byte> memoryKey = key.ToArray();

        if (_cacheMap.TryGetValue(memoryKey, out var node))
        {
            _usageOrder.Remove(node);
            _usageOrder.AddFirst(node);
            return node.Value;
        }

        throw new KeyNotFoundException("The key was not found in the cache.");
    }

    /// <summary>
    /// Tries to retrieve the value from the cache by the given key.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    /// <param name="value">The value of the item if found.</param>
    /// <returns>Returns true if found, otherwise false.</returns>
    public bool TryGetValue(ReadOnlySpan<byte> key, out ReadOnlyMemory<byte>? value)
    {
        ReadOnlyMemory<byte> memoryKey = key.ToArray();

        if (_cacheMap.TryGetValue(memoryKey, out var node))
        {
            value = node.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Clears the entire cache.
    /// </summary>
    public void Clear()
    {
        _cacheMap.Clear();
        _usageOrder.Clear();
    }

    private void EvictLeastUsedItem()
    {
        var lastNode = _usageOrder.Last!;
        _cacheMap.Remove(lastNode.Value);
        _usageOrder.RemoveLast();
    }
}