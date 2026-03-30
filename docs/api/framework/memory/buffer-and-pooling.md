# Buffer and Pooling

This page covers the shared lease and pooling APIs used across networking and dispatch code.

## Source mapping

- `src/Nalix.Common/Abstractions/IBufferLease.cs`
- `src/Nalix.Framework/Memory/Buffers/BufferLease.cs`
- `src/Nalix.Framework/Memory/Pools/ObjectPool.cs`
- `src/Nalix.Framework/Memory/Objects/ObjectPoolManager.cs`
- `src/Nalix.Framework/Memory/Objects/TypedObjectPoolAdapter.cs`

## Main types

- `IBufferLease`
- `BufferLease`
- `ObjectPool`
- `ObjectPoolManager`
- `TypedObjectPoolAdapter<T>`

## IBufferLease and BufferLease

`IBufferLease` is the shared contract. `BufferLease` is the main concrete implementation used by listeners, dispatch, and SDK receive paths.

## Basic usage

```csharp
using BufferLease lease = BufferLease.Rent(1024);

payload.CopyTo(lease.SpanFull);
lease.CommitLength(payload.Length);

ReadOnlyMemory<byte> memory = lease.Memory;
```

### Public methods that matter

- `Retain()`
- `CommitLength(length)`
- `ReleaseOwnership(out buffer, out start, out length)`
- `Dispose()`
- `Rent(capacity, zeroOnDispose)`
- `CopyFrom(span, zeroOnDispose)`
- `FromRented(buffer, length, zeroOnDispose)`
- `TakeOwnership(buffer, start, length, zeroOnDispose)`

## ByteArrayPool

`BufferLease.ByteArrayPool` is the static byte-array rent/return facade used by `BufferLease`.

## Source mapping

- `src/Nalix.Framework/Memory/Buffers/BufferLease.cs`

It resolves the backing implementation once:

- if `BufferPoolManager` is registered, calls are routed there
- otherwise it falls back to `ArrayPool<byte>.Shared`

This makes it the lowest-level pooled byte-array abstraction used by manual buffer workflows.

Useful methods:

- `Rent(capacity)`
- `Return(array)`

## BufferPoolState

`BufferPoolState` is the lightweight diagnostic snapshot for one pool bucket.

## Source mapping

- `src/Nalix.Framework/Memory/Buffers/BufferPoolState.cs`

It exposes:

- `BufferSize`
- `TotalBuffers`
- `FreeBuffers`
- `Misses`
- `CanShrink`
- `NeedsExpansion`
- `GetUsageRatio()`
- `GetMissRate()`

Use it when you need structured pool telemetry instead of just a string report. It is especially useful for dashboards, health checks, and tuning `BufferConfig`.

## ObjectPool

`ObjectPool` is the low-level reusable object store for `IPoolable` instances.

## Basic usage

```csharp
MyPoolable item = ObjectPool.Default.Get<MyPoolable>();
ObjectPool.Default.Return(item);
```

Useful public methods:

- `Get<T>()`
- `Return<T>(obj)`
- `Prealloc<T>(count)`
- `SetMaxCapacity<T>(maxCapacity)`
- `GetTypeInfo<T>()`
- `GetStatistics()`
- `Clear()`
- `ClearType<T>()`
- `Trim(percentage)`
- `GetMultiple<T>(count)`
- `ReturnMultiple<T>(objects)`

## ObjectPoolManager

`ObjectPoolManager` is the higher-level manager with reporting, health checks, typed adapters, and multi-pool statistics.

## Example

```csharp
var adapter = objectPoolManager.GetTypedPool<MyPoolable>();

MyPoolable item = adapter.Get();
adapter.Return(item);

string report = objectPoolManager.GenerateReport();
```

Useful public methods:

- `Get<T>()`
- `Return<T>(obj)`
- `GetTypedPool<T>()`
- `Prealloc<T>(count)`
- `SetMaxCapacity<T>(maxCapacity)`
- `GetTypeInfo<T>()`
- `ClearPool<T>()`
- `ClearAllPools()`
- `TrimAllPools(percentage)`
- `PerformHealthCheck()`
- `ResetStatistics()`
- `ScheduleRegularTrimming(interval, percentage, ct)`
- `GenerateReport()`
- `GetDetailedStatistics()`

## BufferConfig

`BufferConfig` is the configuration object for pool sizing, trimming, adaptive growth, and memory limits.

## Source mapping

- `src/Nalix.Framework/Memory/Buffers/BufferConfig.cs`

It is the place to tune:

- total buffer count across pools
- trim cadence and deep-trim cadence
- adaptive pool growth and shrink thresholds
- hard memory caps and percentage-based caps
- secure clearing on return
- allocation layout through `BufferAllocations`

Important properties include:

- `TotalBuffers`
- `EnableMemoryTrimming`
- `TrimIntervalMinutes`
- `DeepTrimIntervalMinutes`
- `AdaptiveGrowthFactor`
- `MaxMemoryPercentage`
- `MaxMemoryBytes`
- `SecureClear`
- `ExpandThresholdPercent`
- `ShrinkThresholdPercent`
- `MinimumIncrease`
- `MaxBufferIncreaseLimit`
- `BufferAllocations`

### Allocation pattern format

`BufferAllocations` uses `size,ratio` pairs separated by semicolons:

```ini
BufferAllocations = 256,0.10; 512,0.15; 1024,0.20; 2048,0.20
```

Rules enforced by `Validate()`:

- sizes must be strictly increasing
- each ratio must be in `(0, 1]`
- total ratio must stay at or below the configured limit
- expand threshold must be lower than shrink threshold

### When clients should care

Reach for `BufferConfig` when you are tuning:

- long-running servers under varying payload sizes
- memory-sensitive deployments
- secure workloads that require zeroing returned buffers
- adaptive pool behavior under burst traffic

## Related APIs

- [Packet Dispatch](../../routing/packet-dispatch.md)
- [Packet Context](../../routing/packet-context.md)
- [Packet Registry](../packets/packet-registry.md)
- [Pooling Options](../../network/options/pooling-options.md)
- [Object Map and Typed Pools](./object-map-and-typed-pools.md)
- [Reader, Writer, and Header Extensions](../packets/reader-writer-and-header-extensions.md)
