# Design Tradeoffs

!!! warning "Advanced Topic"
    This page explains the architectural decisions behind Nalix. Understanding these tradeoffs will help you decide if Nalix is the right fit for your project.

Nalix is built for high-performance, real-time networking. Achieving this goal required making several intentional tradeoffs that prioritize **runtime efficiency** and **predictability** over **development convenience**.

## 1. Performance over Convenience

Nalix is designed to handle millions of packets per second with minimal GC pressure.

- **The Tradeoff:** We use explicit buffer pooling (`BufferLease`) and object pooling for hot-path contexts.
- **Pros:** Ultra-low latency, stable memory usage under heavy load, and high throughput.
- **Cons:** Developers must be disciplined with resource disposal (using `finally` blocks or `using` statements) to avoid pool depletion.

---

## 2. Explicit over Implicit

Nalix avoids "magic" and hidden side-effects common in higher-level frameworks.

- **The Tradeoff:** We use an explicit `InstanceManager` (Service Locator) instead of standard Dependency Injection for hot-path resolutions. We also require explicit registration of opcodes and handlers.
- **Pros:** Predictable execution order, zero reflection on the hot path, and easier debugging of the "wiring."
- **Cons:** More initial setup code (boilerplate) compared to frameworks that use auto-scanning or reflection-heavy DI.

---

## 3. Modular Layers over Monolithic Integration

Nalix is split into focused packages (`Common`, `Framework`, `Runtime`, `Network`).

- **The Tradeoff:** You must explicitly manage dependencies between layers.
- **Pros:** Leaner binaries for clients, ability to swap the transport (TCP to UDP) without touching business logic, and clear separation of concerns.
- **Cons:** Slightly steeper learning curve to understand which package owns which functionality.

---

## 4. Binary Efficiency over Human Readability

We use a custom binary format rather than JSON or Protobuf for the default `IPacket` implementation.

- **The Tradeoff:** Packets are essentially byte arrays mapped to attributes.
- **Pros:** Minimal CPU cycles spent on serialization, smaller frame sizes on the wire.
- **Cons:** Packets are not human-readable without specialized tooling/logging, and manual binary layout management (`[SerializeOrder]`) is required.

## Summary Decision Matrix

| You should choose Nalix if... | You might prefer another tool if... |
|:---|:---|
| You need sub-millisecond latency. | You are building a standard REST API. |
| You want zero-allocation data paths. | Development speed is more important than CPU efficiency. |
| You need fine-grained control over sharding. | You want a "plug and play" solution with minimal config. |
| Your client and server must share contracts. | You only control one side of the connection. |

## Related Architecture
- [Architecture Overview](../architecture.md) — The layered system design.
- [Performance Optimizations](../performance-optimizations.md) — Details on zero-alloc paths.
