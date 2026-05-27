# Nalix.Analyzers

> Roslyn-based static code analyzer enforcing high-performance coding standards, serialization correctness, and resource safety in Nalix projects.

**Nalix.Analyzers** is the developer guardrail of the Nalix networking framework. It provides real-time feedback in your IDE and during CI/CD to identify API pitfalls, enforce allocation-free code paths, and guarantee binary serialization correctness.

## Key Features

| Feature | Description | Key Target |
| :--- | :--- | :--- |
| ⚡ **Performance Auditing** | Identifies heap allocations (`new` keywords) and boxing in performance-critical message routing pathways. | Hot Path Methods |
| 📦 **Serialization Safety** | Enforces `SerializeOrder` uniqueness, explicit layout boundaries, and header region integrity. | [SerializePackable] Types |
| 🛡️ **Opcode Integrity** | Prevents duplicate routing opcodes globally or locally and flags system-reserved opcode range usage. | [PacketController] Handlers |
| 💧 **Resource Leak Prevention** | Tracks the lifecycle of pooled `IBufferLease` leases to prevent memory leakage or double disposals. | IBufferLease variables |

---

## Core Diagnostic Rules

The analyzer defines a rich catalog of diagnostic checks across four main categories. Below are the most critical diagnostic rules enforced:

| ID | Title | Severity | Target Area | Diagnostic Description / Trigger |
| :--- | :--- | :---: | :---: | :--- |
| **`NALIX001`** | Duplicate Controller Opcode | **Warning** | Routing | Multiple handler methods inside the same controller share a duplicate `PacketOpcode`. |
| **`NALIX002`** | Missing Handler Opcode | **Warning** | Routing | A method matches handler signature patterns but is missing a `[PacketOpcode]` annotation. |
| **`NALIX003`** | Invalid Handler Signature | **Warning** | Routing | A controller method has a signature that is not compatible with `PacketHandlerCompiler`. |
| **`NALIX008`** | Missing Controller Attribute | **Warning** | Routing | A type registered as a dispatch handler is missing the `[PacketController]` attribute. |
| **`NALIX009`** | Missing Static Deserialize | **Warning** | Serialization | A packet type is registered in the registry but is missing a `public static T Deserialize(ReadOnlySpan<byte>)` method. |
| **`NALIX010`** | Generic Self-Type Mismatch | **Warning** | Type Safety | A packet class inherits from `PacketBase<TSelf>` but fails to use itself as the `TSelf` argument. |
| **`NALIX013`** | Missing SerializeOrder | **Warning** | Serialization | A type declares `SerializeLayout.Explicit` but a serializable property has no `[SerializeOrder]` or `[SerializeIgnore]`. |
| **`NALIX014`** | Duplicate SerializeOrder | **Warning** | Serialization | Two properties or fields inside the same class declare the exact same `SerializeOrder` index. |
| **`NALIX020`** | ResetForPool Missing Base Call | **Warning** | Lifecycle | A packet class overrides `ResetForPool()` but does not call `base.ResetForPool()`, potentially leaking headers. |
| **`NALIX022`** | Member Overlaps Header | **Warning** | Serialization | A packet property uses a `SerializeOrder` index that overlaps the reserved 10-byte header region. |
| **`NALIX035`** | Reserved Opcode Range | **Warning** | Routing | A handler is mapped to an opcode in the range `0x0000 - 0x00FF`, which is strictly reserved for system packets. |
| **`NALIX037`** | Potential Allocation in Hot Path | **Info** | Performance | A high-frequency routing hot path contains a class allocation (`new` keyword). Should use `ObjectPoolManager`. |
| **`NALIX039`** | Potential IBufferLease Leak | **Warning** | Lifecycle | A local variable of type `IBufferLease` might not be returned/disposed on all execution pathways. |

---

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Analyzers` | Root namespace containing Roslyn diagnostic analyzers analyzing syntax nodes and semantic models | `NalixUsageAnalyzer` |
| `Nalix.Analyzers.Diagnostics` | Definitions and rule metadata for all compiler and IDE diagnostic codes | `DiagnosticDescriptors` |

## Installation

This package is installed as a development dependency. It executes within the compiler and IDE process and does not add runtime assembly weight:

```bash
dotnet add package Nalix.Analyzers
```

The analyzer will automatically provide real-time suggestions in Visual Studio, Rider, and VS Code. Most diagnostics include automated **Quick Fixes** provided by `Nalix.Analyzers.CodeFixes`.

## IDE Diagnostics and Warnings

- **Real-Time Analysis:** Suggestions and error squiggles appear live as you type.
- **CI/CD Integration:** Warnings and errors are captured during compilation (`dotnet build`), ensuring that high-performance standards are enforced during continuous integration before code is merged.
