# Internals

You do not need this section to use Nalix. Read it only if you want to know how the framework works under the hood.

Everything here is correct and kept up to date, but it goes deeper than most people need: wire formats, the security handshake, sharding, memory pooling, and the guarantees the runtime makes internally. If you just want to build a server or client, start with [Quick Start](../../quickstart.md) and the main [Concepts](../index.md) pages instead.

## What's here

- **How the pieces fit together**: [Architecture](architecture.md), [Packet Lifecycle](packet-lifecycle.md), [Real-time Engine](real-time-engine.md)
- **Wire format and errors**: [Binary Specification](binary-spec.md), [Errors and Diagnostics](errors-and-diagnostics.md)
- **Security in depth**: [Security Architecture](security-architecture.md), [Encryption Model](encryption-model.md), [Handshake Protocol](handshake-protocol.md), [Session Resumption](session-resumption.md)
- **Performance and scale**: [Sharding and Concurrency](sharding.md), [Zero-Allocation Path](zero-allocation.md), [Performance Optimizations](performance-optimizations.md), [Dynamic Concurrency Adjustment](dynamic-concurrency-adjustment.md)
- **Memory management**: [BufferLease Utilization](buffer-lease-utilization.md), [Buffer Pooling Configuration](buffer-pooling-configuration.md)
- **Reliability and guarantees**: [Reliability Model](reliability.md), [System Guarantees](guarantees-and-invariants.md), [Failure Handling](failure-handling.md), [Design Tradeoffs](design-tradeoffs.md)
- **Advanced guides**: [Manual Wiring (No Hosting)](minimal-server.md), [Low-Level Session APIs](low-level-session-apis.md), [Zero-Allocation Hot Path](zero-allocation-hot-path-guide.md)
