# Typed Object Pools

Typed Object Pools provide a high-performance, type-safe facade for interacting with the `ObjectPoolManager`. They eliminate generic dispatch overhead and provide direct access to specific object buckets.

## Typed Adapter Layering

The following diagram shows how the `TypedObjectPoolAdapter<T>` sits between your application and the central `ObjectPoolManager`.

```mermaid
graph LR
    App[Application Code] -- Direct Call --> Adapter[TypedObjectPoolAdapter T]
    Adapter -- Optimized Access --> Pool[Internal Type Bucket]
    Pool -- Metrics and Trimming --> Manager[ObjectPoolManager Global]
```

## Source Mapping

- `src/Nalix.Framework/Memory/Objects/TypedObjectPool.cs`
- `src/Nalix.Framework/Memory/Objects/TypedObjectPoolAdapter.cs`

## Main Types

### TypedObjectPool<T>
A standalone, typed wrapper around a single `ObjectPool`. Best used when you manage your own pool instances manually.

### TypedObjectPoolAdapter<T>
The standard high-performance wrapper for pools registered with the `ObjectPoolManager`. This is the **preferred** way to access pooled objects in the Nalix Framework.

## Key API Members

| Member | Description |
| :--- | :--- |
| `Get()` | Retrieves a fresh instance of `T`. |
| `Return(obj)` | Resets and returns an instance to the pool. |
| `Prealloc(count)` | Warm up the pool by pre-creating instances. |
| `GetMultiple(count)` | Batch retrieval of objects into an array. |
| `ReturnMultiple(objs)` | Batch return of objects to the pool. |
| `Trim(percentage)` | Releases a percentage of idle objects to the GC. |

## Recommended Performance Pattern

For maximum throughput, store the adapter in a `static readonly` or `private readonly` field to avoid repeated manager lookups.

```csharp
private static readonly TypedObjectPoolAdapter<DataPacket> _packetPool = 
    ObjectPoolManager.Instance.GetTypedPool<DataPacket>();

public void SendData()
{
    var packet = _packetPool.Get();
    try 
    { 
        // Use packet...
    }
    finally 
    { 
        _packetPool.Return(packet); 
    }
}
```

## Comparison: When to use which?

| Feature | TypedObjectPool<T> | TypedObjectPoolAdapter<T> |
| :--- | :--- | :--- |
| **Central Management** | No | Yes (via ObjectPoolManager) |
| **Metrics Tracking** | Limited | Full (Hits, Misses, Outstanding) |
| **Global Trimming** | No | Yes |
| **Best Scenario** | Standalone utility classes | Framework hot-paths & Handlers |

## Related APIs

- [Object Pooling](./object-pooling.md)
- [Object Map](./object-map.md)
- [Buffer Management](./buffer-management.md)
