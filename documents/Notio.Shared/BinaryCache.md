# BinaryCache Class

The `BinaryCache` class implements a simple in-memory cache with a specified capacity. It stores items in a least-recently-used (LRU) order, ensuring that the least used items are evicted when the cache reaches its capacity.

## Constructors

### `BinaryCache(int capacity)`

Initializes a new instance of the `BinaryCache` class with the specified capacity.

#### Parameters

- `capacity`: The maximum number of items the cache can hold.

## Properties

### `int Capacity`

Gets the maximum number of items that the cache can hold. This is set during the creation of the cache.

## Methods

### `void Add(ReadOnlySpan<byte> key, ReadOnlyMemory<byte> value)`

Adds an item to the cache. If the item already exists, it updates the value and moves the item to the front.

#### Parameters

- `key`: The key for the item.
- `value`: The value for the item to be stored.

### `ReadOnlyMemory<byte> GetValue(ReadOnlySpan<byte> key)`

Retrieves the value from the cache for the specified key.

#### Parameters

- `key`: The key for the item to retrieve.

#### Returns

- The value associated with the specified key.

#### Exceptions

- `KeyNotFoundException`: Thrown if the key is not found in the cache.

### `bool TryGetValue(ReadOnlySpan<byte> key, out ReadOnlyMemory<byte>? value)`

Attempts to retrieve the value from the cache for the specified key. Returns `true` if the key exists, otherwise `false`.

#### Parameters

- `key`: The key for the item to retrieve.
- `value`: The value associated with the key, if found.

#### Returns

- `true` if the key is found, otherwise `false`.

### `void Clear()`

Clears all items in the cache, effectively resetting it.

### Private Methods

#### `void EvictLeastUsedItem()`

Removes the least recently used item from the cache when the capacity is exceeded.

## Example Usage

```csharp
var cache = new BinaryCache(3);

cache.Add(new ReadOnlySpan<byte>(new byte[] { 1, 2, 3 }), new ReadOnlyMemory<byte>(new byte[] { 4, 5, 6 }));

var value = cache.GetValue(new ReadOnlySpan<byte>(new byte[] { 1, 2, 3 }));

// Output: 04-05-06
Console.WriteLine(BitConverter.ToString(value.ToArray()));  
```

## Notes

- The cache uses an LRU (Least Recently Used) strategy to evict the least recently accessed items when the cache is full.
- ``ReadOnlyMemory<byte>`` is used for storing byte arrays to avoid unnecessary memory allocations.

### Summary

- **Overview**: Provides a brief description of the class.
- **Methods**: Each method is explained with parameters, return values, and exceptions (if any).
- **Example Usage**: Demonstrates how to use the `BinaryCache` class with simple code.
