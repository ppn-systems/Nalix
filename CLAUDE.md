# Nalix — Claude Code Skills

This directory contains per-project Claude Code skills for the **Nalix** ecosystem — a modular, high-performance networking framework for .NET 10.

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

## Skills Index

Each skill contains: **Triggers** (when to use it), **Rules** (invariants from source), **Checklists** (step-by-step for common tasks), **Gotchas** (non-obvious bugs with mechanisms).

| Skill | Project | Focus |
| :--- | :--- | :--- |
| [nalix-abstractions](skills/nalix-abstractions.md) | `Nalix.Abstractions` | Adding contracts; `[SerializeOrder]` rules; `IConnection` auth pattern |
| [nalix-environment](skills/nalix-environment.md) | `Nalix.Environment` | `BufferLease` ref counting; `DataReader` ref struct; fragment assembler |
| [nalix-codec](skills/nalix-codec.md) | `Nalix.Codec` | Packet type definition; transform pipeline order; crypto invariants |
| [nalix-analyzers-generators](skills/nalix-analyzers-generators.md) | `Nalix.Analyzers.Generators` | Generator triggers; `KnownNames` first rule; debug generated output |
| [nalix-framework](skills/nalix-framework.md) | `Nalix.Framework` | `InstanceManager` singleton-only; pool `Reset()` completeness; Snowflake IDs |
| [nalix-runtime](skills/nalix-runtime.md) | `Nalix.Runtime` | Handler shape; protocol ordering invariant; middleware stages; throttle tiers |
| [nalix-network](skills/nalix-network.md) | `Nalix.Network` | Session timing; `ConnectionGuard` ban tiers; UDP anti-replay; session resume |
| [nalix-hosting](skills/nalix-hosting.md) | `Nalix.Hosting` | Startup sequence; auto-registered handlers; middleware vs handler ordering |
| [nalix-logging](skills/nalix-logging.md) | `Nalix.Logging` | `NLogix` facade; shutdown flush; what must never be logged |
| [nalix-sdk](skills/nalix-sdk.md) | `Nalix.SDK` | Client connect flow; `On<T>()` vs `RequestAsync`; reconnect gotchas |
| [nalix-sdk-native](skills/nalix-sdk-native.md) | `Nalix.SDK.Native` | C ABI constraints; error-code pattern; publish RIDs |
| [nalix-analyzers](skills/nalix-analyzers.md) | `Nalix.Analyzers` | `NAL0xxx` ranges; adding diagnostic rules; zero-alloc constraint |
| [nalix-analyzers-codefixes](skills/nalix-analyzers-codefixes.md) | `Nalix.Analyzers.CodeFixes` | MEF discovery; trivia preservation; adding code fixes |
| [documentation](skills/documentation.md) | `Documentation` | MkDocs rules R1–R20; signature validation; reusable prompt template |

## Global Rules

- **Language:** C# 14 on .NET 10 (`net10.0`). Analyzers/generators target `netstandard2.0`.
- **Namespaces:** File-scoped only.
- **Nullable:** Enabled everywhere — never disable.
- **Classes:** Prefer `sealed` unless inheritance is required.
- **Structs:** Prefer `readonly struct`.
- **XML docs:** Required on all public APIs.
- **Hot paths:** Zero-allocation. Use `Span<T>`, pooled buffers, no LINQ.
- **Security:** Never invent crypto. Reuse existing primitives in `Nalix.Codec.Security`. Never log secrets.
- **Subclasses:** Large classes are split into `.cs`, `.Types.cs`, `.Cleanup.cs`, `.Report.cs` files — please follow this pattern consistently — and only split when the file is larger than 800 lines.

## Build & Test

- **Build:** `dotnet build src/Nalix.sln --configuration Release`
- **Test:** `dotnet test tests/Nalix.Tests.sln --configuration Release`
- Run build and test only if files under `src/` or `tests/` were modified.
- Changes outside `src/` and `tests/` do not require validation.
- Do not run build or test for documentation, workflow, repository metadata, or other non-source changes.

## Coding Behavior

Guidelines derived from [Andrej Karpathy's observations](https://x.com/karpathy/status/2015883857489522876) on LLM coding pitfalls. Bias toward caution over speed; use judgment for trivial tasks.

### Think Before Coding

- State assumptions explicitly before implementing. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so and push back.
- If something is unclear, stop and name what's confusing.

### Simplicity First

- No features beyond what was asked. No speculative abstractions.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If 200 lines could be 50, rewrite it.

### Surgical Changes

- Touch only what the task requ
ires. Don't improve adjacent code or formatting.
- Match existing style even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.
- Remove imports/variables/functions that **your** changes made unused. Don't remove pre-existing dead code unless asked.
- Every changed line should trace directly to the user's request.

### Goal-Driven Execution

- Transform tasks into verifiable goals before starting.
- For multi-step tasks, state a brief plan with a verification step per stage.
- Strong success criteria allow independent looping. Weak criteria ("make it work") require constant clarification.
