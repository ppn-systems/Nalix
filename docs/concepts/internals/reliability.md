# Reliability Model

!!! warning "Advanced Topic"
    This page provides a high-level summary of the Nalix reliability framework. It serves as the "Confidence Layer" for architects deciding whether to trust Nalix for production workloads.

Nalix is built to be a resilient foundation for real-time systems. Our reliability model is built on three pillars: **Deterministic Execution**, **Fault Isolation**, and **Resource Discipline**.

## Source Mapping

- `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs`
- `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs`
- `src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs`
- `src/Nalix.Abstractions/Networking/IConnectionErrorTracked.cs`
- `tests/Nalix.Framework.Tests/Memory/BufferPoolTests.cs`

## Pillar 1: Deterministic Execution

Nalix eliminates the "hidden races" common in multi-threaded networking by enforcing strict execution invariants.

- **Connection Affinity:** Per-connection queueing and dispatch ordering are designed to keep one client's backlog from turning into arbitrary cross-connection races.
- **Fixed Pipeline:** The order in which security, throttling, and logic are applied is deterministic and based on your startup configuration.

!!! success "Production Confidence"
    Handlers are effectively single-threaded per connection. You can scale to millions of connections across many worker threads while maintaining a simple mental model for your business logic.

---

## Pillar 2: Fault Isolation

The Nalix runtime treats user code as "potentially unstable" and provides a safety net to prevent cascading failures.

- **Non-Stop Workers:** An exception in one handler will **never** stop the background worker loops or affect other clients.
- **Observable Health:** Every failure is tracked via `IConnection.ErrorCount` (from the `IConnectionErrorTracked` interface). Runtime and middleware layers can use that signal to identify and disconnect "poison" clients automatically.

!!! success "Production Confidence"
    A single bug in a handler or a malformed packet will not crash your server. The system is designed to "shed" failing work and keep the infrastructure healthy.

---

## Pillar 3: Resource Discipline

Memory leaks and pool exhaustion are the silent killers of long-running servers. Nalix enforces strict resource management.

- **Guaranteed Cleanup:** Using `try-finally` internally, Nalix ensures that every byte of memory leased from a pool is returned, even if a handler crashes or a network connection is lost mid-request.
- **Backpressure Ready:** Bounded queues and drop/block policies provide explicit pressure control instead of allowing unbounded growth by default.

!!! success "Production Confidence"
    Nalix can run for months without memory drift or GC-related latency spikes, provided you follow the simple rule of disposing of any objects you manually lease.

---

## Technical Audit Trail

Architects can verify these claims by auditing the following core components:

| Component | Source Reference | Responsibility |
|:---|:---|:---|
| **Dispatch Channel** | `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs` | Ordering and Affinity |
| **Worker Loop** | `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs` | Fault Isolation & Cleanup |
| **ErrorHandler** | `src/Nalix.Runtime/Dispatching/PacketDispatcherBase.cs` | Client Signaling (FAIL) |
| **Pools** | `tests/Nalix.Framework.Tests/Memory/BufferPoolTests.cs` | Buffer Integrity |

## Summary Decision

If your production requirements demand **sub-millisecond latency**, **guaranteed packet order**, and **resilience against handler faults**, Nalix is architected from the ground up to meet those needs.
