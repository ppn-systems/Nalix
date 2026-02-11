# Object Map and Typed Pools

This page covers the reusable object helpers around `ObjectMap<TKey, TValue>`, `TypedObjectPool<T>`, and `TypedObjectPoolAdapter<T>`.

!!! note "Implementation detail"
    These types are useful when you need lower-level pool-aware containers and typed pool access, but they are intentionally kept out of the main nav to avoid overloading the API tree.

## Source mapping

- `src/Nalix.Framework/Memory/Objects/ObjectMap.cs`
- `src/Nalix.Framework/Memory/Objects/TypedObjectPool.cs`
- `src/Nalix.Framework/Memory/Objects/TypedObjectPoolAdapter.cs`

## Main types

- `ObjectMap<TKey, TValue>`
- `TypedObjectPool<T>`
- `TypedObjectPoolAdapter<T>`

## Public members at a glance

| Type | Public members |
|---|---|
| `ObjectMap<TKey, TValue>` | `Rent()`, `Return()`, `ResetForPool()`, `TryGetValue(...)`, `ContainsKey(...)`, `Add(...)`, `Remove(...)`, `Clear()` |
| `TypedObjectPool<T>` | `Get()`, `Return(...)`, `Prealloc(...)`, `Clear()`, `GetMultiple(...)`, `ReturnMultiple(...)`, `GetInfo()`, `SetMaxCapacity(...)` |
| `TypedObjectPoolAdapter<T>` | `Get()`, `Return(...)`, `GetMultiple(...)`, `ReturnMultiple(...)`, `Trim(...)`, `Clear()`, `Prealloc(...)`, `GetInfo()`, `SetMaxCapacity(...)` |

## ObjectMap<TKey, TValue>

`ObjectMap<TKey, TValue>` is a thread-safe pooled dictionary built on `ConcurrentDictionary<TKey, TValue>`.

Use it when you want:

- concurrent key/value access
- a reusable map instance rented from the object pool
- snapshot-style enumeration without hand-managing dictionary allocation churn

Important members:

- `Rent()`
- `Return()`
- `ResetForPool()`
- `TryGetValue(key, out value)`
- `ContainsKey(key)`
- `Add(key, value)`
- `Remove(key)`
- `Clear()`

### When to use ObjectMap

Use it when:

- you need a pooled concurrent dictionary
- the container stays alive for the life of a connection or session
- you want a reusable map without paying repeated allocation costs

Avoid it when:

- the map is tiny and ephemeral
- you do not need concurrent access
- you do not have a clear return/disposal boundary

## Basic usage

```csharp
ObjectMap<string, int> map = ObjectMap<string, int>.Rent();
try
{
    map.Add("users", 10);
    bool found = map.TryGetValue("users", out int count);
}
finally
{
    map.Return();
}
```

## TypedObjectPool<T>

`TypedObjectPool<T>` is the direct typed facade over `ObjectPool`.

It is useful when you want type-safe access to one pool bucket without repeatedly passing generic calls through the parent pool.

Important members:

- `Get()`
- `Return(obj)`
- `Prealloc(count)`
- `Clear()`
- `GetMultiple(count)`
- `ReturnMultiple(objects)`
- `GetInfo()`
- `SetMaxCapacity(maxCapacity)`

### Performance guidance

- preallocate in startup if the pool is part of a steady hot path
- return objects promptly so the pool can stay warm
- keep reset logic cheap; expensive resets erase the benefit of pooling

## TypedObjectPoolAdapter<T>

`TypedObjectPoolAdapter<T>` is the `ObjectPoolManager`-aware typed facade.

Compared with `TypedObjectPool<T>`, it also participates in manager-level operation accounting.

Important members:

- `Get()`
- `Return(obj)`
- `GetMultiple(count)`
- `ReturnMultiple(objects)`
- `Trim(percentage)`
- `Clear()`
- `Prealloc(count)`
- `GetInfo()`
- `SetMaxCapacity(maxCapacity)`

### Common pitfalls

- using a pool type that cannot safely reset the object between uses
- returning an object after some other code already assumed ownership
- overusing pooling for objects that are not actually hot-path allocations

## When to use which

- use `ObjectMap<TKey, TValue>` when you need a pooled concurrent dictionary
- use `TypedObjectPool<T>` when you already own an `ObjectPool` and want typed access
- use `TypedObjectPoolAdapter<T>` when you are working through `ObjectPoolManager` and want typed access plus manager metrics

## Related APIs

- [Buffer and Pooling](./buffer-and-pooling.md)
- [Configuration and DI](../runtime/configuration.md)
