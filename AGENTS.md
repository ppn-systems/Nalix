# Nalix — Agent Instructions

Rules and context for AI agents working on the **Nalix** codebase — a modular, high-performance networking framework for .NET 10.

---

## Dependency Graph

```plaintext
Level 0 : Nalix.Abstractions            (zero deps)
Level 0 : Nalix.Analyzers               (Roslyn only, netstandard2.0)
Level 0 : Nalix.Analyzers.Generators    (Roslyn only, netstandard2.0)
Level 1 : Nalix.Analyzers.CodeFixes     → Analyzers
Level 1 : Nalix.Environment             → Abstractions
Level 2 : Nalix.Codec                   → Abstractions, Environment, Analyzers.Generators (generator)
Level 2 : Nalix.Framework               → Abstractions, Environment, Codec
Level 3 : Nalix.Runtime                 → Abstractions, Framework, Codec
Level 3 : Nalix.Network                 → Abstractions, Framework
Level 3 : Nalix.Logging                 → Abstractions, Framework
Level 3 : Nalix.SDK                     → Codec
Level 4 : Nalix.Hosting                 → Abstractions, Framework, Codec, Runtime, Network
Level 5 : Nalix.SDK.Native              → SDK (Native AOT, C ABI)
```

**NEVER introduce circular references or skip dependency levels.**

---

## Coding Rules

- **Language:** C# 14 on .NET 10 (`net10.0`). Analyzers/generators target `netstandard2.0`.
- **Namespaces:** File-scoped only (`namespace Foo.Bar;`).
- **Nullable:** Enabled everywhere — never add `#nullable disable`.
- **Classes:** Prefer `sealed` unless inheritance is explicitly required.
- **Structs:** Prefer `readonly struct`.
- **XML docs:** Required on all `public` APIs.
- **Hot paths:** Zero-allocation. Use `Span<T>`, pooled buffers — no LINQ, no `new byte[]`.
- **Security:** Never invent cryptographic primitives. Reuse `Nalix.Codec.Security` only. Never log keys, tokens, or secrets.
- **Partial classes:** Large classes use `.cs` + `.Types.cs` + `.Cleanup.cs` + `.Report.cs` — follow this split consistently.
- **DI:** Use `InstanceManager` (singleton-only). Never `Microsoft.Extensions.DependencyInjection`.

---

## Critical Invariants

### Protocol ordering
`KEY_EXCHANGE` → `CLIENT_HELLO` → `CLIENT_FINISH` → application packets

Violating this order causes a hard `Disconnect()` at runtime.

### Transform pipeline order
Outbound: serialize → compress → encrypt
Inbound: decrypt → decompress → deserialize

Never swap compress and encrypt.

### Session snapshot timing
Session is saved at `CLIENT_FINISH` (handshake completion). Never on disconnect, never lazily.

### Packet opcode uniqueness
Opcodes must be globally unique across all `PacketBase<T>` subclasses. Collision is silent at compile time.

### Handler method signature
Handler methods must be `public static async ValueTask` — instance methods are not resolved.

### `IPoolable.Reset()`
Must clear **every** mutable field. Partial reset causes data leaks between callers. No validation catches this.

### `BufferLease` async handoff
Call `Retain()` before any async handoff. Omitting it causes use-after-free when the sender disposes the lease.

### `[UnmanagedCallersOnly]` boundary
No exceptions may cross the native boundary. Every exported method must `try/catch` and return an error code.

---

## Build & Test

```bash
# Build
dotnet build src/Nalix.sln --configuration Release

# Test
dotnet test tests/Nalix.Tests.sln --configuration Release
```

- Run build and test only when files under `src/` or `tests/` are modified.
- Documentation, workflow, and metadata changes do not require build/test.

---
