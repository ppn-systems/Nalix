# Glossary

This page defines the core Nalix terms that appear across the documentation. Use it as a quick reference when API pages become implementation-heavy.

---

## BufferLease

A pooled byte buffer rented from `BufferPoolManager`. Must be disposed after use to return the buffer to the pool. Used throughout the hot path to avoid `byte[]` allocation.

## Connection

`Connection` is the runtime session object for one remote client. It holds:

- Connection ID
- Remote endpoint
- TCP/UDP transport adapters
- Permission level
- Cipher and secret state
- Runtime counters (bytes sent, uptime, ping time, error count)

## ConnectionHub

`ConnectionHub` is the in-memory registry of active connections. Use it for:

- Connection lookup by ID or username
- Forced disconnects
- Bulk broadcast
- Connection-level reporting via `GenerateReport()`

## Dispatch

In the docs, "dispatch" usually means `PacketDispatchChannel` and its associated options and runtime. This is the component that:

- Queues incoming work across sharded workers
- Deserializes packets via the packet registry
- Runs middleware chains
- Invokes handlers
- Processes supported return types

## InstanceManager

The runtime service registry used across the Nalix stack. A service-locator pattern optimized for allocation-free resolution. Not a traditional DI container — designed for performance on the networking hot path.

## Metadata Provider

A component that adds extra metadata to handler methods during `PacketMetadata` construction. Implement `IPacketMetadataProvider` when handler attributes alone are not sufficient and you need conventions or custom policy tags.

## Middleware

Logic inserted before or around handler execution using the **`MiddlewarePipeline`**. It operates on `IPacketContext<TPacket>` after packet deserialization, with full access to the packet and handler metadata.

## PacketBase\<T\>

The base class for all Nalix packets. Provides system header management (magic, opcode, protocol, priority) and pooling support via `ResetForPool()`.

## PacketContext

`PacketContext<TPacket>` is the per-request object passed to context-aware handlers and packet middleware. It provides access to:

- The deserialized packet
- The current connection
- Resolved packet metadata (attributes)
- Cancellation token
- A pooled sender for manual replies

## PacketPool

`PacketPool<TPacket>` / `PacketLease<TPacket>` — Thread-safe pooling for reusable packet instances. Rent a packet, populate it, send it, then dispose the lease to return it.

## PacketRegistry

An immutable, `FrozenDictionary`-backed catalog of packet deserializers built by `PacketRegistryFactory`. Provides O(1) lookup by magic number (FNV-1a hash of the packet type's full name).

## Protocol

`Protocol` is the bridge between a live connection and the dispatch pipeline. In practice it:

- Accepts or rejects new connections via `ValidateConnection()`
- Starts receive loops
- Forwards incoming message frames into dispatch via `ProcessMessage()`
- Controls whether connections stay open after processing

## Return Handler

The internal component that translates a handler's return type into a send action. Supported return types include:

- `TPacket` / `Task<TPacket>` / `ValueTask<TPacket>`
- `string`
- `byte[]` / `Memory<byte>`
- `void` / `Task` / `ValueTask` (no response)

## Snowflake

A customized 56-bit distributed identifier used for internal task tracking and packet correlation. Provides 1 ms timestamp resolution with 12 bits for sequence (4,096 IDs per millisecond per shard).

## TCP vs UDP

| Transport | Use when |
| :--- | :--- |
| **TCP** | Reliable, ordered request/response. The default and recommended starting point. |
| **UDP** | Low-latency datagrams where packet loss is acceptable. Requires pre-established session identity and authentication. |

## TimingWheel

The idle-timeout scheduler used by the network layer. Manages connection timeouts with O(1) scheduling complexity. Detects and closes dead or inactive connections efficiently.

---

## Recommended Next Pages

- [Selecting Building Blocks](./runtime/building-blocks.md) — Decision guide for component selection
- [Architecture](./fundamentals/architecture.md) — Layered component overview
- [Middleware](./runtime/middleware-pipeline.md) — Middleware Pipeline and handler policy
