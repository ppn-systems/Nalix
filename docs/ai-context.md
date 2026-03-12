# Nalix AI Context

Nalix is an enterprise-grade, real-time TCP/UDP networking framework for .NET 10. This document provides a high-density technical summary for AI agents assisting in development, debugging, or architecture.

## Architecture Layers
1. **`Nalix.Common`**: Shared contracts and attributes. **Rule:** Never add runtime dependencies here.
2. **`Nalix.Framework`**: Infrastructure implementations (Serialization, Security, Pooling, DI).
3. **`Nalix.Runtime`**: The "Brain". Negotiates between packets and logic.
4. **`Nalix.Network`**: The "Body". Raw I/O, Sockets, and Protocol framing.
5. **`Nalix.SDK`**: Client-side transport abstractions.

## Golden Rules for Code Contribution
1. **No New Hot-Path Allocations:** Always use `BufferLease` and pooled objects. Avoid `Task` if `ValueTask` is possible.
2. **Connection Affinity is Sacrosanct:** Do not break the sequential processing of packets per connection.
3. **Fault Isolation:** Handlers must not leak exceptions to the worker threads. Wrap top-level logic in `try-catch`.
4. **Explicit over Implicit:** Prefer explicit service resolution through `InstanceManager` over magic DI.
5. **Memory Hygiene:** Every leased buffer **must** be disposed of in a `finally` block.

## Core Execution Flow (Trace)
1. **Ingest:** `SocketConnection` reads bytes → `NetworkPipe`.
2. **Frame:** `IProtocol` parses bytes → `IBufferLease`.
3. **Dispatch:** `PacketDispatchChannel` dequeues → locates `PacketMetadata`.
4. **Pipeline:** Raw buffer → `NetworkBufferMiddleware` (Decryption/Decompression).
5. **Deserialize:** `LiteSerializer` converts buffer → `IPacket` object.
6. **Logic:** `IPacketMiddleware` (Auth/RateLimit) → `PacketHandler`.
7. **Cleanup:** `PacketContext` and `IBufferLease` are disposed.

## Common Pitfalls (Anti-patterns)
- **Locking in Handlers:** High-concurrency workers should use lock-free patterns or connection-affinity guarantees.
- **Manual Byte Manipulation:** Use `LiteReader` / `LiteWriter` extensions for binary I/O.
- **Ignoring CancellationToken:** Always propagate tokens to prevent ghost tasks on disconnect.

## Implementation Secrets (Deep-Dives)
- **Priority Dispatch:** `DispatchChannel` doesn't just dequeue; it checks `PriorityReadyQueue` based on `PacketPriority`. Workers use `SpinWait` followed by `SemaphoreSlim` to balance latency/CPU.
- **Header Structure:** [2B Length][1B OpCode][8B SeqID/Metadata]. Bodies are 8-byte aligned for SIMD-friendly processing.
- **Registry Freezer:** `PacketRegistry` freezes at startup into an array-backed lookup if opcodes are dense, otherwise `FrozenDictionary` for $O(1)$ access.
- **Middleware Ordering:** Buffer-middleware (decryption/checksum) runs **before** deserialization. Packet-middleware (auth/logic) runs **after**.
- **The "Directive" System:** System-level frames (0x0...0xF) bypass the normal dispatch channel for emergency signals (SHUTDOWN, HEARTBEAT).

## Shared Data Layouts
- **`BufferLease`:** A wrapper around `Memory<byte>` with a `PoolNode` reference. **Safety:** Must use `_lease.Memory` only within the scope of the handler.
- **`PacketContext`:** Pooled class containing `IConnection`, `Metadata`, and `TransportCancellationToken`.

## Service Orchestration (`InstanceManager`)
- **Static Resolution:** Services are resolved via `InstanceManager.Get<T>()`.
- **Dynamic Scopes:** For per-request dependencies, use the `Properties` dictionary on `IConnection` instead of creating DI scopes.

## Essential Tooling & Commands
- **Test:** `dotnet test` (Unit/Integration).
- **Benchmark:** `dotnet run -c Release --project benchmarks/Nalix.Benchmark.Framework/`
- **Docs:** `mkdocs serve` (Requires Python + Material theme).

## Decision Matrix for AI Assistance
- **Logic?** → Handler.
- **Cross-cutting?** → Middleware.
- **Wire Format?** → `IProtocol`.
- **Performance?** → Check `DispatchChannel` or `LiteSerializer`.
- **Security?** → Check `PacketCipher` or `AEAD` envelopes.
- **Bootstrap?** → Check `NetworkApplicationBuilder`.
