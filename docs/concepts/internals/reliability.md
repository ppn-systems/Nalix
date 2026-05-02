# Reliability Model

!!! warning "Advanced Topic"
    This page provides a high-level summary of the Nalix reliability framework. It serves as the "Confidence Layer" for architects deciding whether to trust Nalix for production workloads.

Nalix is built to be a resilient foundation for real-time systems. Our reliability model is built on three pillars: **Deterministic Execution**, **Fault Isolation**, and **Resource Discipline**.

## 🛡️ Pillar 1: Deterministic Execution

Nalix eliminates the "hidden races" common in multi-threaded networking by enforcing strict execution invariants.

- **Connection Affinity:** You can trust that packets from one client are handled sequentially. This prevents race conditions in your business logic without requiring complex synchronization code.
- **Fixed Pipeline:** The order in which security, throttling, and logic are applied is deterministic and based on your startup configuration.

!!! success "Production Confidence"
    Handlers are effectively single-threaded per connection. You can scale to millions of connections across many worker threads while maintaining a simple mental model for your business logic.

---

## 🛑 Pillar 2: Fault Isolation

The Nalix runtime treats user code as "potentially unstable" and provides a safety net to prevent cascading failures.

- **Non-Stop Workers:** An exception in one handler will **never** stop the background worker loops or affect other clients.
- **Observable Health:** Every failure is tracked via `IConnection.ErrorCount` (from the `IConnectionErrorTracked` interface) and reported via the `Directive` signaling system. This allows your monitoring systems to identify and disconnect "poison" clients automatically.

!!! success "Production Confidence"
    A single bug in a handler or a malformed packet will not crash your server. The system is designed to "shed" failing work and keep the infrastructure healthy.

---

## ♻️ Pillar 3: Resource Discipline

Memory leaks and pool exhaustion are the silent killers of long-running servers. Nalix enforces strict resource management.

- **Guaranteed Cleanup:** Using `try-finally` internally, Nalix ensures that every byte of memory leased from a pool is returned, even if a handler crashes or a network connection is lost mid-request.
- **Backpressure Ready:** Our MPMC dispatch rings provide natural backpressure. If your handlers are too slow, the ingestion buffers will fill up, eventually signaling the OS to throttle the TCP window, rather than growing the heap indefinitely.

!!! success "Production Confidence"
    Nalix can run for months without memory drift or GC-related latency spikes, provided you follow the simple rule of disposing of any objects you manually lease.

---

## Technical Audit Trail

Architects can verify these claims by auditing the following core components:

| Component | Source Reference | Responsibility |
|:---|:---|:---|
| **Dispatch Channel** | [DispatchChannel.cs](https://github.com/ppn-systems/nalix/blob/master/src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs) | Ordering and Affinity |
| **Worker Loop** | [PacketDispatchChannel.cs](https://github.com/ppn-systems/nalix/blob/master/src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs) | Fault Isolation & Cleanup |
| **ErrorHandler** | [PacketDispatcherBase.cs](https://github.com/ppn-systems/nalix/blob/master/src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs) | Client Signaling (FAIL) |
| **Pools** | [BufferPoolTests.cs](https://github.com/ppn-systems/nalix/blob/master/tests/Nalix.Framework.Tests/Memory/BufferPoolTests.cs) | Buffer Integrity |

## Summary Decision

If your production requirements demand **sub-millisecond latency**, **guaranteed packet order**, and **resilience against handler faults**, Nalix is architected from the ground up to meet those needs.
