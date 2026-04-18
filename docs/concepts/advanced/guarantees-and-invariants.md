# Guarantees and Invariants

!!! warning "Advanced Topic"
    This page describes the low-level contracts and reliability guarantees of the Nalix runtime. If you are just getting started, we recommend reading the [Architecture](../architecture.md) and [Quickstart](../../quickstart.md) first.

When building high-concurrency networked systems, developers need to know exactly what the platform guarantees and what is left to the application logic. This page defines the operational contracts of the Nalix framework.

## 1. Sequential Connection Processing

Nalix guarantees that packets arriving from the **same connection** are processed strictly in order.

- **FIFO Ordering:** If Packet A arrives before Packet B on Connection 1, the handler for Packet A will be invoked before the handler for Packet B.
- **Mutual Exclusion:** No two handlers for the same connection will ever execute concurrently. This means you do not need to use `lock` or other synchronization primitives when accessing connection-specific state within a handler.

### Scope and Boundaries

- **Scope:** This guarantee is per-connection and applies within a single server instance.
- **Non-Guarantee:** Nalix does **not** guarantee ordering across different connections. Connection A and Connection B are handled in parallel by different background workers.

!!! info "Implementation Detail"
    This is achieved in `DispatchChannel.cs` through an internal `readyFlag` state machine. When a worker pulls a connection from the ready queue, it "locks" the connection's readiness until the current packet processing is complete.

---

## 2. Middleware Execution Order

Nalix guarantees that the middleware pipeline executes in the exact order of registration.

- **Linear Pipeline:** If you register `AuthMiddleware` then `LoggingMiddleware`, the request will hit Auth, then Logging.
- **Short-circuiting:** Any middleware can terminate the request by returning a `Directive` packet (e.g., `FAIL` or `UNAUTHORIZED`).

---

## 3. Thread-Safety Invariants

The Nalix runtime components are designed for high-concurrency ingestion and are internally thread-safe.

- **Thread-Safe Dispatch:** The `PacketDispatchChannel` supports concurrent packet pushing from multiple I/O loops without external locking.
- **Registry Safety:** The `IPacketRegistry` and `InstanceManager` are thread-safe for resolution after initialization.

!!! info "Implementation Detail"
    Nalix uses lock-free MPMC (Multi-Producer Multi-Consumer) ring buffers and atomic counters to ensure high throughput without global mutex contention.

---

## 4. Response Routing Contract

Nalix guarantees that a response returned from a handler will always be routed back to the connection that originated the request.

- **Connection Binding:** Every `PacketContext<TPacket>` is bound to the live `IConnection` instance throughout the dispatch lifecycle.
- **Implicit Send:** Returning an `IPacket` or `ValueTask<IPacket>` from a handler automatically serializes and sends it back to the mapped connection.

---

## 5. Non-Guarantees (Explicit Boundaries)

To avoid common pitfalls, be aware of what Nalix does **NOT** guarantee:

- **At-Least-Once Delivery:** For UDP transports, Nalix is "best-effort." Reliable delivery must be implemented at the application or protocol layer if needed.
- **Global Ordering:** There is no global "clock" or order guarantee for packets arriving from different clients.
- **Automatic Retry:** If a handler throws an exception, Nalix will log it and discard the packet. It will **not** automatically retry the execution.

## Related Source Code

- [PacketDispatchChannel.cs](file:///e:/Cs/Nalix/src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs) — Worker loop and wake-up signaling.
- [DispatchChannel.cs](file:///e:/Cs/Nalix/src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs) — Connection-affinity and priority queuing.
- [PacketDispatcherBase.cs](file:///e:/Cs/Nalix/src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs) — Internal handler execution and error wrap.
