# Guarantees and Failure Handling

!!! warning "Advanced Topic"
    This page describes the low-level contracts, reliability model, and failure behavior of the Nalix runtime. If you are just getting started, read [Architecture](./architecture.md) and the [Quickstart](../../quickstart.md) first.

When building high-concurrency networked systems, you need to know exactly what the platform guarantees, what happens when something goes wrong, and what is left to your application logic. This page covers all three.

## Source Mapping

- `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs`
- `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs`
- `src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs`
- `src/Nalix.Abstractions/Networking/IConnectionErrorTracked.cs`
- `tests/Nalix.Framework.Tests/Memory/BufferPoolTests.cs`

## What the runtime guarantees

### Sequential connection processing

Packets arriving from the **same connection** are processed strictly in order.

- **FIFO ordering** — if Packet A arrives before Packet B on Connection 1, the handler for Packet A runs before the handler for Packet B.
- **Mutual exclusion** — no two handlers for the same connection ever execute concurrently. You don't need `lock` or other synchronization to access connection-specific state within a handler.
- **Scope** — this guarantee is per-connection, within a single server instance. Nalix does **not** guarantee ordering across different connections; Connection A and Connection B are handled in parallel by different background workers.

!!! info "Implementation detail"
    Achieved in `DispatchChannel.cs` through an internal `readyFlag` state machine. When a worker pulls a connection from the ready queue, it "locks" the connection's readiness until the current packet finishes processing.

### Middleware execution order

The middleware pipeline executes in the order determined by the `[MiddlewareOrder]` attribute — ascending for inbound, descending for outbound. Registration order is secondary to the declared order. Any middleware can terminate the request early by returning a `Directive` packet (e.g. `FAIL` or `UNAUTHORIZED`).

### Thread-safety invariants

- **Thread-safe dispatch** — `PacketDispatchChannel` supports concurrent packet pushing from multiple I/O loops without external locking.
- **Registry safety** — `PacketRegistry` and `InstanceManager` are thread-safe for resolution after initialization.

!!! info "Implementation detail"
    Nalix uses lock-free MPMC (Multi-Producer Multi-Consumer) ring buffers and atomic counters for high throughput without global mutex contention.

### Response routing contract

A response returned from a handler is always routed back to the connection that originated the request. Every `PacketContext<TPacket>` is bound to the live `IConnection` instance throughout the dispatch lifecycle — returning an `IPacket` or `ValueTask<IPacket>` from a handler automatically serializes and sends it back to the mapped connection.

### Non-guarantees (explicit boundaries)

- **At-least-once delivery** — for UDP transports, Nalix is best-effort. Reliable delivery must be implemented at the application or protocol layer if you need it.
- **Global ordering** — there is no global clock or order guarantee across packets from different clients.
- **Automatic retry** — if a handler throws, Nalix logs it and discards the packet. It does **not** automatically retry execution.

## Reliability model

The reliability model rests on three pillars: deterministic execution, fault isolation, and resource discipline.

### Deterministic execution

Connection affinity (per-connection queueing) and a fixed pipeline order (security, throttling, and logic applied deterministically per your startup configuration) eliminate the hidden races common in multi-threaded networking. Handlers are effectively single-threaded per connection — you can scale to millions of connections across many worker threads while keeping a simple mental model for your business logic.

### Fault isolation

The runtime treats user code as potentially unstable. An exception in one handler never stops the background worker loops or affects other clients. Every failure is tracked via `IConnection.ErrorCount` (the `IConnectionErrorTracked` interface); runtime and middleware layers can use that signal to identify and disconnect "poison" clients automatically. A single bug in a handler or a malformed packet will not crash your server.

### Resource discipline

Using `try-finally` internally, Nalix ensures every byte of memory leased from a pool is returned, even if a handler crashes or the connection drops mid-request. Bounded queues and drop/block policies provide explicit backpressure instead of unbounded growth. Given that you dispose any objects you manually lease, Nalix can run for months without memory drift or GC-related latency spikes.

## What happens on failure

### Fault isolation in practice

- **Handler exceptions** — caught by `PacketDispatcherBase`. The request is aborted, but the worker thread stays healthy and continues processing the next packet in the queue.
- **Pipeline faults** — if a middleware throws, the rest of the pipeline is skipped for that packet only.

Observable behavior: an `Error`-level log is emitted via the configured `ILogger`; `IConnection.ErrorCount` is atomically incremented via `connection.IncrementErrorCount()`; the server attempts to send a `Directive` packet with `ControlType.FAIL` and `Reason = INTERNAL_ERROR` to the client.

### Deserialization grace

Malformed incoming data is intercepted at the earliest possible stage:

- **OpCode mismatch** — an opcode not registered in the `PacketRegistry` causes the frame to be discarded, with a `Warning` logged.
- **Binary corruption** — a failed deserialization (bit flip, missing field) is caught internally as a `SerializationFailureException`.

Observable behavior: the client receives a `Directive` with `Reason = REQUEST_INVALID`.

### Lifecycle aborts (timeouts & disconnects)

Nalix uses `CancellationToken` propagation so resources aren't held by abandoned requests.

- **Client disconnect** — the `IConnection` state is marked inactive. `PacketDispatchChannel` automatically drains the pending packet queue for that connection and cancels any currently executing handlers via `PacketContext.CancellationToken`.
- **Execution timeouts** — if `TimeoutMiddleware` is present, it cancels the request's token after the configured duration.

!!! info "Implementation detail"
    `DispatchChannel.cs` listens to the `ConnectionUnregistered` event from `IConnectionHub` to trigger immediate cleanup of per-connection queues.

### Resource discipline on failure

Regardless of success or failure, cleanup is guaranteed: every `BufferLease` is disposed via a `try-finally` block in the dispatch loop, preventing leaks in `BufferPoolManager`; `PacketContext` objects are recycled to their internal pools after handler execution regardless of outcome.

### Summary of effects

| Event | Effect on server | Effect on client | Visibility |
| :--- | :--- | :--- | :--- |
| Handler exception | Worker continues; error count++ | Receives FAIL directive | `ILogger` (Error) |
| Malformed packet | Packet discarded; error count++ | Receives INVALID directive | `ILogger` (Warning) |
| Disconnect | Queue drained; handlers cancelled | Connection closed | `IConnectionHub` event |
| Serialization fail | Frame discarded; error count++ | Receives INVALID directive | `ILogger` (Error) |

## Technical Audit Trail

| Component | Source reference | Responsibility |
|:---|:---|:---|
| Dispatch Channel | `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs` | Ordering, affinity, connection-unregistered cleanup |
| Worker Loop | `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs` | Fault isolation & buffer cleanup |
| ErrorHandler | `src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs` | Handler exception wrap, client FAIL/INVALID signaling |
| Pools | `tests/Nalix.Framework.Tests/Memory/BufferPoolTests.cs` | Buffer integrity |
| `ErrorCount` contract | `src/Nalix.Abstractions/Networking/IConnectionErrorTracked.cs` | Per-connection error signal |

If your production requirements demand sub-millisecond latency, guaranteed per-connection packet order, and resilience against handler faults, this is the model Nalix is built around.
