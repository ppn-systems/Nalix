using System;

namespace Notio.Shared.Memory.Caches;

/// <summary>
/// Represents a binary cache that inherits from the Least Recently Used (LRU) cache.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BinaryCache"/> class with the specified capacity.
/// </remarks>
/// <param name="capacity">The maximum number of elements the cache can hold.</param>
public sealed class BinaryCache(int capacity) : LruCache<byte[], ReadOnlyMemory<byte>>(capacity)
{
}
