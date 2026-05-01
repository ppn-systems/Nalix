# Performance Optimizations

!!! warning "Advanced Topic"
    This page describes internal framework mechanics like Span limits, structure alignments, and GC overheads.

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Advanced
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Architecture](../fundamentals/architecture.md)

Nalix is engineered to minimize latency and maximize throughput on the networking hot path. This page explains the specific techniques used and why they matter for production workloads.

## 1. Zero-Allocation Data Path

Traditional networking stacks suffer from GC pressure due to frequent buffer allocations. Nalix eliminates this by pooling all hot-path resources.

!!! tip
    Monitor GC pause time and allocated bytes as your primary performance indicators during load testing.

For a complete end-to-end walkthrough of how these optimizations work together in a production scenario, see the [Zero-Allocation Design](./zero-allocation.md) guide.

### Buffer Pooling (Slab-Based)

Instead of allocating `byte[]` per request, Nalix uses a slab-based `BufferPoolManager`. Every incoming packet is leased into a segment of a large, pre-allocated memory slab (`ArraySegment<byte>`). This ensures strict $O(1)$ lease/release performance and zero heap fragmentation.

- **Pinned Memory Slabs** â€” Eliminates **POH (Pinned Object Heap)** churn by allocating large blocks once, significantly reducing Gen 1 GC pauses.
- **Lock-free slab allocation** â€” Minimizes thread contention during high-frequency leasing using thread-local caches.
- **Atomic Lease Tracking** â€” `BufferLease` instances are pooled using a lock-free free-list with an **O(1) atomic counter**, avoiding the linear-time overhead of traditional collection count checks.
- **Span-first API** â€” Leverages `Span<byte>` and `ReadOnlySpan<byte>` for slicing without copying data.
- **Deterministic lifetime** â€” `BufferLease` implements `IDisposable`, ensuring buffers return to the slab after handler execution.

### Poolable Contexts (IPacketContext)

The `PacketContext<TPacket>` object itself is poolable. When a handler is invoked, the context is fetched from a thread-safe pool and reset after the handler completes. This avoids per-request allocations for the most frequently created object in the dispatch path.

## 2. Dedicated OS Thread Dispatching

To minimize context-switching overhead and maximize CPU cache affinity, Nalix binds its dispatch loops to dedicated OS threads rather than the standard `.NET ThreadPool`.

```mermaid
graph LR
    Incoming["Incoming Packets"] --> Shard0["Worker 0 (Core 0)"]
    Incoming --> Shard1["Worker 1 (Core 1)"]
    Incoming --> ShardN["Worker N (Core N)"]
    Shard0 --> Handler0["Handler"]
    Shard1 --> Handler1["Handler"]
    ShardN --> HandlerN["Handler"]
```

- **Processor Affinity** â€” Dispatch workers can be configured to stay on specific logical CPU cores, reducing L1/L2 cache misses.
- **Managed Drain Budget** â€” A "drain budget" ensures that each wake cycle processes a batch of packets before yielding, balancing latency and throughput.

- **Parallel execution** â€” Workers are scaled to match logical CPU cores in auto mode.
- **Low-latency wake** â€” Uses specialized signaling to wake dedicated threads immediately upon packet arrival, bypassing the non-deterministic scheduling of the standard thread pool.

## 3. 64-bit Snowflake Identifiers

Nalix uses a customized 64-bit Snowflake identifier for internal task tracking and packet correlation.

| Design choice | Rationale |
| :--- | :--- |
| 64-bit (vs. standard 64-bit) | Fits efficiently into packed headers, avoids 53-bit precision limits in JavaScript-based clients |
| 1 ms timestamp resolution | Sufficient for networking use cases; enables 4,096 IDs per millisecond per shard (12-bit sequence) |
| Deterministic ordering | Snowflake IDs are sortable by creation time, enabling natural ordering in logs and diagnostics |

## 4. Frozen Registry Lookups

The `PacketRegistry` uses `System.Collections.Frozen.FrozenDictionary<uint, PacketDeserializer>` for packet type resolution.

- **O(1) access** â€” Immutable, read-optimized lookup tables built once at startup.
- **Function-pointer binding** â€” Packet deserialization is bound using `delegate* managed<ReadOnlySpan<byte>, TPacket>` (unsafe function pointers). This eliminates delegate allocation and reduces indirection compared to `Func<>` delegates.
- **FNV-1a magic keys** â€” Packet types are identified by a 32-bit FNV-1a hash of the type's full name, computed during registry construction.

## 5. Metadata Pre-Compilation

Middleware and handler metadata are not resolved via reflection on every request.

- **Compiled handlers** â€” Handler methods are wrapped in pre-compiled delegates during `Build()`. No reflection occurs during handler invocation.
- **Attribute caching** â€” Packet metadata (permissions, timeouts, rate limits, concurrency limits) is resolved once during handler registration and cached alongside the packet entry in the registry.

## 6. LZ4 Compression

The `LZ4Codec` provides pooled block compression and decompression optimized for networking payloads.

- **Pooled hash tables** â€” `LZ4HashTablePool` manages reusable hash tables to avoid allocation during compression.
- **Span-based API** â€” Both `Encode` and `Decode` accept `ReadOnlySpan<byte>` input and `Span<byte>` output, supporting zero-copy integration with the buffer pool.
- **Lease-based output** â€” `Encode(input, out BufferLease lease, out int bytesWritten)` produces a pooled buffer lease ready for direct network transmission.

## Maintaining Performance in Your Application

To preserve these performance characteristics in your own handlers and middleware:

1. **Always dispose `BufferLease` and `PacketScope<T>`** â€” Leaking pooled resources degrades throughput over time.
2. **Avoid blocking in handlers** â€” Use `async`/`await` for I/O. For scheduled work, use `TaskManager` or `TimingWheel` instead of `Task.Delay`.
3. **Prefer `ValueTask` for handler return types** â€” Avoids unnecessary `Task` allocations on synchronous (already-complete) code paths.
4. **Use `IPacketContext.Packet`** â€” Access the deserialized packet from the context rather than creating new instances.

## Benchmarks

For measured performance data across serialization, cryptography, compression, and infrastructure, see the [Benchmarks](../../benchmarks/index.md) section.

## Recommended Next Pages

- [Architecture](../fundamentals/architecture.md) â€” Layered component overview
- [Packet System](../fundamentals/packet-system.md) â€” Serialization layouts and wire format
- [Buffer Management](../../api/framework/memory/buffer-management.md) â€” Buffer pool API details
- [Object Pooling](../../api/framework/memory/object-pooling.md) â€” Object recycling API details
- [LZ4](../../api/codec/lz4.md) â€” Compression API details

