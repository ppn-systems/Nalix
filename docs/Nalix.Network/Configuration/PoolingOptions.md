# PoolingOptions — Resource Pooling Configuration for .NET Network Servers

`PoolingOptions` centralizes configuration for object pooling in the network connection layer (e.g., for TCP listeners/handlers using pooled contexts or socket async arguments).  
This enables low-GC, high-concurrency, and predictable resource use in scalable .NET server backends.

---

## Purpose

- **Prevent excessive allocations:** Large networks often create and destroy thousands of connection/packet/socket context objects per second.
- **Startup performance:** Preallocate as much pool capacity as feasible to reduce startup latency and first-request overhead.
- **Production scaling:** Tune pool sizes (max/prealloc) to match expected/peak network load.

---

## Core Properties

| Property                     | Description                                                                  | Default |
|------------------------------|------------------------------------------------------------------------------|---------|
| **AcceptContextMaxCapacity** | Max pooled `PooledAcceptContext` instances (per listener or server process). | 1024    |
| **PacketContextMaxCapacity** | Max pooled `PacketContext<T>` instances.                                     | 1024    |
| **SocketArgsMaxCapacity**    | Max pooled `PooledSocketAsyncEventArgs` instances.                           | 1024    |
| **AcceptContextPreallocate** | On startup, preallocate this many `PooledAcceptContext`.                     | 16      |
| **PacketContextPreallocate** | On startup, preallocate this many `PacketContext<T>`.                        | 16      |
| **SocketArgsPreallocate**    | On startup, preallocate this many socket args objects.                       | 16      |

**All values can be set via configuration, code, or bound using DI/config frameworks.**

---

## Validation & Constraints

- Max capacities are 1–1,000,000 (default 1024).
- Preallocation must not exceed max capacity for each object kind.
- `Validate()` throws exception if any property violates its range or consistency.

---

## Usage Example

```csharp
PoolingOptions options = new PoolingOptions
{
    AcceptContextMaxCapacity = 2048,
    SocketArgsPreallocate = 128,
    // ... others as needed
};
options.Validate();

// Pass to pool manager, listener, or DI for entire server stack
ObjectPoolManager.Configure(options);
```

---

## Tuning Guidance

- **For high-throughput servers**, set max capacity a little *above* expected connection peak.
- **Preallocate** a realistic amount for startup (avoid spikes on first live load).
- On cloud or test/dev, keep pools more conservative to save memory.
- Watch GC metrics! Pool "starvation" or aggressive evictions indicate under-sizing.

---

## Typical Object Types

- `PooledAcceptContext`: Used during socket accept; lifecycle is per incoming client connection
- `PacketContext<T>`: State & metadata for inbound/outbound message handling; reusable per-request
- `PooledSocketAsyncEventArgs`: Core for async network IO in .NET Sockets (crucial for performance)

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025 PPN Corporation.

---

## See Also

- [NetworkSocketOptions.md](./NetworkSocketOptions.md)
- [TcpListenerBase documentation](../Listeners/TcpListenerBase.md)
- [ObjectPoolManager reference](../../Nalix.Shared/Memory/ObjectPoolManager.md)
