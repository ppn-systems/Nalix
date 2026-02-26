# Nalix.Analyzers

Roslyn-based static analysis to enforce high-performance coding standards and serialization correctness in Nalix projects.

## Features

- **Serialization Validation**: Catches overlapping orders, missing attributes, and forbidden types in packet definitions.
- **Hot-Path Enforcement**: Warns about heap allocations and boxing in performance-critical methods.
- **OpCode Verification**: Ensures OpCode values are synchronized with documentation and registry logic.
- **Buffer Leak Protection**: Detects potential `IBufferLease` leaks in middlewares and handlers.

## Installation

```bash
dotnet add package Nalix.Analyzers
```

## Supported Diagnostics

| ID | Title | Level |
|---|---|---|
| `NALIX013` | Explicit serialization member missing Order | Error |
| `NALIX014` | Duplicate SerializeOrder | Error |
| `NALIX022` | Member overlaps header region | Warning |
| `NALIX001` | Allocation in Hot Path | Warning |

## Documentation

View the full [Diagnostic Catalog](https://ppn-systems.me/api/analyzers/diagnostic-codes) for explanations and fix suggestions.
