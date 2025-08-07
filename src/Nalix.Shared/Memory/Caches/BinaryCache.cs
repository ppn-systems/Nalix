namespace Nalix.Shared.Memory.Caches;

/// <summary>
/// Represents a binary cache that inherits from the Least Recently Used (LRU) cache.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BinaryCache"/> class with the specified capacity.
/// </remarks>
/// <param name="capacity">The maximum TransportProtocol of elements the cache can hold.</param>
public sealed class BinaryCache(System.Int32 capacity)
    : LruCache<System.Byte[], System.ReadOnlyMemory<System.Byte>>(capacity)
{
}
