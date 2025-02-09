# LRU Cache

This repository contains an implementation of a Least Recently Used (`LRU`) cache in C#.

## Introduction

An LRU (Least Recently Used) Cache is a type of cache that removes the least recently used items when the cache reaches its capacity. This implementation uses a dictionary to store the cache items and a linked list to keep track of their usage order.

## Usage

To use the LRU Cache, include the `LRUCache` class in your project and initialize it with the desired capacity. Below is an example of how to use this cache:

```csharp
using Notio.Shared.Memory.Cache;

var cache = new LRUCache<string, int>(capacity: 2);

cache.Add("key1", 1);
cache.Add("key2", 2);
Console.WriteLine(cache.GetValue("key1")); // Output: 1

cache.Add("key3", 3);
// At this point, "key2" should be evicted because the cache reached its capacity
try
{
    Console.WriteLine(cache.GetValue("key2")); // Throws KeyNotFoundException
}
catch (KeyNotFoundException)
{
    Console.WriteLine("Key not found");
}
```

### Key Components

- `CacheItem Class`: A private class used to store the key-value pairs.
- `Dictionary`: Used to store the cache items for O(1) access.
- `LinkedList`: Used to keep track of the usage order of the items.

### Methods

- `Add(TKey key, TValue value)`: Adds an item to the cache. If the cache is full, it removes the least recently used item.
- `GetValue(TKey key)`: Retrieves the value associated with the specified key. Moves the item to the front of the list to mark it as most recently used.
- `TryGetValue(TKey key, out TValue value)`: Tries to get the value associated with the specified key. Returns true if found, false otherwise.
- `Clear()`: Clears all items from the cache.

## Contributing

If you would like to contribute to this project, please fork the repository and submit a pull request. For major changes, please open an issue first to discuss what you would like to change.
