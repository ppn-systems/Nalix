using System;

namespace Nalix.Shared.Memory.Caches;

/// <summary>
/// Represents a binary cache that inherits from the Least Recently Used (LRU) cache.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BinaryCache"/> class with the specified capacity.
/// </remarks>
/// <param name="capacity">The maximum ProtocolType of elements the cache can hold.</param>
public sealed class BinaryCache(Int32 capacity) : LruCache<Byte[], ReadOnlyMemory<Byte>>(capacity)
{
}
