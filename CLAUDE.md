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
| [documentation](skills/documentation.md) | `Documentation` | Technical writing, formatting rules, MkDocs |


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

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **nalix** (8690 symbols, 22803 relationships, 300 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/nalix/context` | Codebase overview, check index freshness |
| `gitnexus://repo/nalix/clusters` | All functional areas |
| `gitnexus://repo/nalix/processes` | All execution flows |
| `gitnexus://repo/nalix/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
