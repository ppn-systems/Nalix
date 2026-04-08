# Architecture

This page describes the high-level architecture of the Nalix server stack, which consists of `Nalix.Network`, `Nalix.Runtime`, and `Nalix.Network.Hosting`.

Use it when you want the big picture before diving into the API pages.

## Runtime map

At a high level, a Nalix server is built from:

- listeners that accept TCP or UDP traffic
- a protocol that decides how accepted traffic is processed
- a dispatch channel that deserializes packets and invokes handlers
- connection services that keep session state and transport helpers alive
- throttling and middleware that protect the runtime under load

```mermaid
flowchart LR
    Client["Client"] --> Listener["TcpListenerBase / UdpListenerBase"]
    Listener --> Protocol["Protocol"]
    Protocol --> Dispatch["PacketDispatchChannel queue + worker loop"]
    Dispatch --> Deserialize["Packet registry deserialize"]
    Deserialize --> Metadata["Resolve handler + metadata"]
    Metadata --> Middleware["Packet middleware"]
    Middleware --> Handler["Handler controller / method"]
    Handler --> Connection["Connection / ConnectionHub"]
```

## Main server flow

The common TCP path looks like this:

1. `TcpListenerBase` accepts a socket.
2. `ConnectionGuard` may reject the endpoint before full admission.
3. The listener creates a `Connection` and passes it to `Protocol.OnAccept(...)`.
4. The protocol forwards framed data into `PacketDispatchChannel`.
5. Dispatch queues the work, the worker loop deserializes the packet, resolves metadata, runs middleware, invokes the handler, and optionally sends a result back through the connection.

The UDP path follows the same broad model, but datagrams are authenticated and mapped to session state differently inside `UdpListenerBase`.

## Core building blocks

### Listeners

- `TcpListenerBase` owns accept loops, backpressure, timing-wheel integration, and connection lifecycle.
- `UdpListenerBase` owns datagram receive loops, auth validation, and UDP session handling.

### Protocol

`Protocol` is the bridge between transport and dispatch. It is where accepted connections are initialized and where incoming message buffers are handed over to application dispatch.

### Dispatch

`PacketDispatchChannel` is the application entry point for packets. It is responsible for:

- queueing inbound work
- running the worker loop
- deserializing packets with the packet registry
- resolving handler descriptors and metadata
- running middleware
- processing handler return values

Under the hood it uses `PacketDispatchOptions<TPacket>` and shard-aware worker loops.

### Connection state

`Connection` and `ConnectionHub` provide:

- live connection lookup
- session/user correlation
- send helpers for TCP and UDP
- diagnostics and cleanup hooks

### Protection and pressure control

The network runtime is designed to run with pressure controls enabled, not as an afterthought.

Typical pieces are:

- `ConnectionGuard` for admission control
- `TokenBucketLimiter` and `PolicyRateLimiter` for request-rate protection
- `ConcurrencyGate` for in-flight handler limits
- `TimingWheel` for timeout and scheduled expiry handling
- `TimeSynchronizer` for periodic time-sync events used by the listener layer

## Dispatch pipeline

Conceptually, packet dispatch looks like this:

```mermaid
sequenceDiagram
    participant L as Listener
    participant P as Protocol
    participant D as PacketDispatchChannel
    participant Q as DispatchQueue
    participant W as WorkerLoop
    participant M as Middleware
    participant H as Handler
    participant C as Connection

    L->>P: accepted data / datagram
    P->>D: handle packet
    D->>Q: enqueue lease
    W->>Q: pull lease
    W->>W: deserialize + resolve metadata
    W->>M: run inbound middleware
    M->>H: invoke handler
    H-->>W: return result or complete
    W-->>C: send response if applicable
```

The exact middleware stack depends on your configuration and metadata. Packet attributes such as permission, timeout, concurrency, and rate limits become runtime behavior through the metadata pipeline.

## Where other packages fit

- `Nalix.Common` provides shared abstractions, packet attributes, and networking primitives.
- `Nalix.Framework` provides core utilities like `ConfigurationManager`, `InstanceManager`, `TaskManager`, `Snowflake`, and serialization helpers.
- `Nalix.Runtime` provides the dispatch pipeline, middleware, and handler compilation.
- `Nalix.Network.Hosting` provides the standard builder and application host.
- `Nalix.SDK` is the client-side counterpart.

## Recommended next pages

- [Real-time Engine](./real-time.md)
- [Middleware](./middleware.md)
- [Packet Dispatch](../api/runtime/routing/packet-dispatch.md)
- [Connection Hub](../api/network/connection/connection-hub.md)
- [Server Blueprint](../guides/server-blueprint.md)
