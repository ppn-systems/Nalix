# Architecture

The Nalix architecture is designed for high-throughput, low-latency networking with a focus on zero-allocation data paths and shard-aware execution. This page explains how the framework's packages relate to each other, how data flows through the system, and where each component sits in the overall design.

## System Context

At the highest level, Nalix is a networking stack that sits between external clients and your application logic.

```mermaid
graph LR
    User("External Client / Service") -- "TCP / UDP" --> Stack("Nalix Networking Stack")
    Stack -- "Logs" --> Logging("NLogix / External SIEM")
    Stack -- "Config" --> Config("Configuration (INI)")
```

## Package Dependency Map

Nalix uses a modular package architecture. Each package has a focused responsibility and a well-defined dependency direction.

```mermaid
graph TD
    Hosting["Nalix.Network.Hosting"] --> Network["Nalix.Network"]
    Hosting --> Runtime["Nalix.Runtime"]
    Runtime --> Framework["Nalix.Framework"]
    Runtime --> Common["Nalix.Common"]
    Network --> Framework
    Network --> Common
    Framework --> Common
    SDK["Nalix.SDK"] --> Framework
    SDK --> Common
    Pipeline["Nalix.Network.Pipeline"] --> Network
    Logging["Nalix.Logging"] --> Common
```

| Layer | Package | Responsibility |
|---|---|---|
| Hosting | `Nalix.Network.Hosting` | Fluent builder, application lifecycle, automatic discovery |
| Transport | `Nalix.Network` | TCP/UDP listeners, connections, protocol bridge, session store |
| Dispatch | `Nalix.Runtime` | Packet dispatch, middleware, handler compilation, session resume |
| Pipeline | `Nalix.Network.Pipeline` | Rate limiting, concurrency gating, time synchronization |
| Infrastructure | `Nalix.Framework` | Configuration, DI, serialization, packet registry, pooling, compression, identifiers |
| Contracts | `Nalix.Common` | Shared abstractions, packet attributes, middleware primitives |
| Client | `Nalix.SDK` | Transport sessions, request/response correlation, handshake and resume flows |
| Logging | `Nalix.Logging` | Structured logging with batched console and file targets |

## Server vs. Client Separation

Nalix enforces a clear separation of concerns between the server host and the client SDK.

**Server (thick host):** The host owns socket listeners, connection management, the connection hub, and the shard-aware dispatch channel. It is responsible for scale (worker sharding), security (admission control, rate limiting), and application orchestration (middleware, handler compilation).

**Client (thin SDK):** The SDK focuses on session management, automated request/response correlation, and transparent encryption/compression. The client does not embed dispatch or middleware infrastructure — it sends and receives packets directly.

**Shared contracts:** Packet definitions (POCOs annotated with `[SerializePackable]`) should live in a shared assembly referenced by both server and client projects.

## The Packet Journey

Understanding how a packet moves through the system is the key to effective debugging and optimization.

```mermaid
sequenceDiagram
    participant Net as Socket / Buffer
    participant Prot as Protocol (Framing)
    participant BufMw as Buffer Middleware
    participant Disp as PacketDispatchChannel
    participant Reg as PacketRegistry
    participant PktMw as Packet Middleware
    participant Hand as Handler

    Net->>Prot: Raw bytes received
    Prot->>Prot: Validate frame (length / checksum)
    Prot->>BufMw: Raw IBufferLease
    BufMw->>Disp: HandlePacket(lease, connection)
    Note over Disp: Shard-aware queueing
    Disp->>Reg: Deserialize (magic → TPacket)
    Reg-->>Disp: Packet instance
    Disp->>PktMw: Execute middleware chain
    PktMw->>Hand: Invoke handler (IPacketContext<T>)
    Hand-->>Disp: Return response
    Disp->>Net: Serialize and send reply
```

## Core Building Blocks

### 1. Transport and Listeners

- **`TcpListenerBase`** — High-concurrency TCP listener using `SocketAsyncEventArgs`. Handles socket acceptance, connection lifecycle, and frame-level receive.
- **`UdpListenerBase`** — Stateless datagram listener with built-in authenticated session mapping.
- **Connection guard** — Early-stage admission control to reject endpoints at the socket level before allocating any application resources.

### 2. Protocol (The Bridge)

The `IProtocol` interface translates raw network streams into discrete message leases. It ensures that `PacketDispatchChannel` only receives complete, valid packet fragments. The protocol also manages connection acceptance (`ValidateConnection`) and initiates the receive loop.

### 3. Shard-Aware Dispatch

`PacketDispatchChannel` is the engine of Nalix. It provides:

- **Worker sharding** — Multiple worker loops (parallel to CPU core count) prevent head-of-line blocking. One slow handler does not stall unrelated packets.
- **Wake-signaling** — Coalesced signaling using `System.Threading.Channels` minimizes thread context switching under bursty load.
- **Prioritization** — Native support for `PacketPriority` (Urgent, System, High, Normal, Low).

### 4. Packet Registry

`PacketRegistry` uses `FrozenDictionary<uint, PacketDeserializer>` for O(1) lookup of packet deserializers by magic number (a stable FNV-1a hash of the packet type's full name). Deserialization is bound using unsafe function pointers (`delegate*`) to eliminate delegate allocation on the hot path.

### 5. Instance Management

Nalix uses `InstanceManager` (a service-locator pattern optimized for allocation-free resolution) instead of standard DI containers. This ensures that shared services — loggers, packet registries, application services — can be resolved during hot-path execution without container overhead or allocation.

## Protection and Pressure Control

The network runtime is designed to run with pressure controls enabled by default:

| Component | Purpose |
|---|---|
| `ConnectionGuard` | Socket-level admission control; rejects endpoints before application resources are allocated |
| `TokenBucketLimiter` | Protects against request spikes with configurable burst and refill rates |
| `PolicyRateLimiter` | Per-opcode and per-endpoint rate limiting driven by handler metadata |
| `ConcurrencyGate` | Limits the number of in-flight handlers to prevent thread pool exhaustion |
| `TimingWheel` | Manages idle timeouts with O(1) scheduling complexity |

## Where Does My Code Go?

| Your code | Which package / layer |
|---|---|
| Packet definitions (POCOs) | Shared contracts assembly → `Nalix.Common` + `Nalix.Framework` |
| Handler classes | Server project → registered with `PacketDispatchChannel` |
| Middleware | Server project → implements `IPacketMiddleware<TPacket>` from `Nalix.Common` |
| Protocol customization | Server project → extends `Protocol` from `Nalix.Network` |
| Client session logic | Client project → uses `TcpSession` from `Nalix.SDK` |
| Configuration | INI file → loaded by `ConfigurationManager` from `Nalix.Framework` |

## Recommended Next Pages

- [Packet Lifecycle](./packet-lifecycle.md) — Step-by-step request path
- [Performance Optimizations](./performance-optimizations.md) — Zero-allocation design details
- [Middleware](./middleware.md) — Buffer vs. packet middleware
- [Real-time Engine](./real-time.md) — Session lifecycle and throttling
- [Production End-to-End](../guides/production-end-to-end.md) — Production server setup
