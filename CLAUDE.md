# Nalix — Claude Code Skills

This directory contains per-project Claude Code skills for the **Nalix** ecosystem — a modular, high-performance networking framework for .NET 10.

## Dependency Graph

```plaintext
Level 0 : Nalix.Abstractions          (zero deps)
Level 0 : Nalix.Analyzers             (Roslyn only, netstandard2.0)
Level 0 : Nalix.Analyzers.CodeFixes   (→ Analyzers)
Level 0 : Nalix.Analyzers.Generators (Roslyn only, netstandard2.0)
Level 1 : Nalix.Environment           → Abstractions
Level 2 : Nalix.Codec                 → Abstractions, Environment, Analyzers.Generators (generator)
Level 2 : Nalix.Framework             → Abstractions, Environment, Codec
Level 3 : Nalix.Runtime               → Abstractions, Framework, Codec
Level 3 : Nalix.Network               → Abstractions, Framework
Level 3 : Nalix.Logging               → Abstractions, Framework
Level 3 : Nalix.SDK                   → Codec
Level 4 : Nalix.Hosting               → Abstractions, Framework, Codec, Runtime, Network
Level 5 : Nalix.SDK.Native            → SDK (Native AOT, C ABI)
```

**NEVER introduce circular references or skip dependency levels.**

## Skills Index

| Skill | Project | Purpose |
| :--- | :--- | :--- |
| [nalix-abstractions](skills/nalix-abstractions.md) | `Nalix.Abstractions` | Contracts, interfaces, attributes, enums |
| [nalix-environment](skills/nalix-environment.md) | `Nalix.Environment` | Bootstrap, memory I/O, config, fragments |
| [nalix-codec](skills/nalix-codec.md) | `Nalix.Codec` | Serialization, crypto, compression, frames |
| [nalix-analyzers-generators](skills/nalix-analyzers-generators.md) | `Nalix.Analyzers.Generators` | Roslyn source generators |
| [nalix-framework](skills/nalix-framework.md) | `Nalix.Framework` | DI, Snowflake, pooling, tasks |
| [nalix-runtime](skills/nalix-runtime.md) | `Nalix.Runtime` | Dispatch, middleware, handlers, throttling |
| [nalix-network](skills/nalix-network.md) | `Nalix.Network` | TCP/UDP transport, connections, sessions |
| [nalix-hosting](skills/nalix-hosting.md) | `Nalix.Hosting` | Builder APIs, application host, lifecycle |
| [nalix-logging](skills/nalix-logging.md) | `Nalix.Logging` | Async logging, sinks, NLogix |
| [nalix-sdk](skills/nalix-sdk.md) | `Nalix.SDK` | Client sessions, request-response |
| [nalix-sdk-native](skills/nalix-sdk-native.md) | `Nalix.SDK.Native` | Native AOT, C ABI interop |
| [nalix-analyzers](skills/nalix-analyzers.md) | `Nalix.Analyzers` | Roslyn diagnostic analyzers |
| [nalix-analyzers-codefixes](skills/nalix-analyzers-codefixes.md) | `Nalix.Analyzers.CodeFixes` | IDE quick-fix providers |

## Global Rules

- **Language:** C# 14 on .NET 10 (`net10.0`). Analyzers/generators target `netstandard2.0`.
- **Namespaces:** File-scoped only.
- **Nullable:** Enabled everywhere — never disable.
- **Classes:** Prefer `sealed` unless inheritance is required.
- **Structs:** Prefer `readonly struct`.
- **XML docs:** Required on all public APIs.
- **Hot paths:** Zero-allocation. Use `Span<T>`, pooled buffers, no LINQ.
- **Security:** Never invent crypto. Reuse existing primitives. Never log secrets.

## Build & Test

- **Build:** `dotnet build src/Nalix.sln --configuration Release`
- **Test:** `dotnet test tests/Nalix.Tests.sln --configuration Release`
- Run build and test only if files under `src/` or `tests/` were modified.
- Changes outside `src/` and `tests/` do not require validation.
- Do not run build or test for documentation, workflow, repository metadata, or other non-source changes.
