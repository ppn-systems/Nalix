# Nalix.Abstractions

> Base abstractions, enums, and shared contracts for the entire Nalix ecosystem.

**Nalix.Abstractions** defines the fundamental interfaces, protocol constants, and primitive types that all other Nalix packages depend on. It is the lowest-level building block — every module in the stack references this package.

## Key Namespaces

| Namespace | Purpose |
| :--- | :--- |
| `Abstractions` | Core interfaces (`IConnection`, `IPacket`, `IPoolable`, `IBufferLease`, etc.) |
| `Networking` | Packet, protocol, and session contracts |
| `Serialization` | Serialization attributes and layout primitives |
| `Security` | Cipher suite enums and permission levels |
| `Identity` | Snowflake ID type definitions |
| `Concurrency` | Shared concurrency primitives |
| `Exceptions` | Framework-wide exception models |
| `Logging` | Logger abstractions |

## Installation

```bash
dotnet add package Nalix.Abstractions
```

> **Note:** You typically don't need to install this package directly — it is referenced transitively by all higher-level Nalix packages.

## Documentation

For detailed API reference, see the [Nalix.Abstractions package guide](https://ppn-systems.me/api/Abstractions/index).
