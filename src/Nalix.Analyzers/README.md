# Nalix.Analyzers

> [!NOTE]
> Roslyn-based static analysis to enforce high-performance coding standards and serialization correctness in Nalix projects.

`Nalix.Analyzers` is a developer-productivity package that identifies common pitfalls when working with the Nalix networking framework. It provides real-time feedback in your IDE and during CI/CD to ensure your application remains efficient, secure, and maintainable.

## Key Features

- **⚡ Performance First**: Detects heap allocations and unnecessary boxing in performance-critical hot paths.
- **📦 Reliable Serialization**: Validates `SerializeOrder` continuity and detects header region overlaps.
- **🛡️ Secure Routing**: Prevents duplicate `PacketOpcode` assignments and enforces system-reserved ranges.
- **💧 Resource Management**: Identifies potential `IBufferLease` leaks before they cause production memory pressure.

## Critical Diagnostics

| ID | Title | Level | Summary |
|---|---|---|---|
| `NALIX013` | Missing `SerializeOrder` | **Error** | Layout is explicit but member has no order index. |
| `NALIX014` | Duplicate `SerializeOrder` | **Error** | Two members share the same order; order must be unique. |
| `NALIX022` | Header Overlap | **Warning** | Payload member overlaps the reserved 12-byte header. |
| `NALIX037` | Hot Path Allocation | **Info** | Method is high-frequency; avoid `new` where possible. |

## Quick Start

Add the analyzer package to your project:

```bash
dotnet add package Nalix.Analyzers
```

The analyzer will automatically start providing suggestions in Visual Studio, Rider, and VS Code. Most diagnostics include **Quick Fixes** to resolve issues with a single click.

## Documentation

For a complete list of all 45+ rules, visit the [Diagnostic Catalog](https://ppn-systems.me/api/analyzers/diagnostic-codes).
