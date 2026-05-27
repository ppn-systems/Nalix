# Nalix.Abstractions

> Base abstractions, enums, and shared contracts for the entire Nalix ecosystem.

**Nalix.Abstractions** defines the fundamental interfaces, protocol constants, and primitive types that all other Nalix packages depend on. It is the lowest-level building block — every module in the stack references this package.

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Abstractions` | Root namespace for core lifecycle, activation, and memory management | `IPoolable`, `IBufferLease`, `IBufferPoolManager`, `IObjectPoolManager`, `IActivatable`, `IWithLogging` |
| `Nalix.Abstractions.Networking` | Essential network connection management and transport configurations | `IConnection`, `IConnectionHub`, `INetworkEndpoint`, `NetworkTransport` |
| `Nalix.Abstractions.Networking.Packets` | Rich packet modeling, metadata attributes, and deserializers | `IPacket`, `IPacketContext`, `PacketOpcodeAttribute`, `PacketTransportAttribute`, `IPacketSender` |
| `Nalix.Abstractions.Networking.Protocols` | Low-level protocol codes and routing advice | `ProtocolOpCode`, `ProtocolAdvice`, `ControlFlags`, `ProtocolReason` |
| `Nalix.Abstractions.Networking.Sessions` | Core abstractions for session lifecycle and storage services | `ISessionStore`, `ISessionService`, `ISessionFactory`, `SessionEntry` |
| `Nalix.Abstractions.Serialization` | Declarative serialization metadata and fixed-size layout primitives | `[SerializeOrder]`, `[SerializeHeader]`, `SerializeLayout`, `IFixedSizeSerializable` |
| `Nalix.Abstractions.Security` | Cryptographic configurations, cipher suite configurations, and access levels | `CipherSuiteType`, `PermissionLevel`, `DropPolicy`, `ISequenceCounter` |
| `Nalix.Abstractions.Identity` | Highly optimized 64-bit distributed snowflake identifier definitions | `ISnowflake`, `SnowflakeType` |
| `Nalix.Abstractions.Concurrency` | Lightweight task executions, background worker/handles, and prioritizations | `ITaskManager`, `IWorker`, `IWorkerHandle`, `WorkerPriority` |
| `Nalix.Abstractions.Exceptions` | Classifiers and exception models tailored for high-speed networking | `BaseException`, `CipherException`, `NetworkException`, `ExceptionClassifier` |
| `Nalix.Abstractions.Diagnostics` | Core observability and performance telemetry contracts | `CoreTelemetryTarget` |
| `Nalix.Abstractions.Middleware` | Cross-cutting packet interceptor and pipeline stages | `IPacketMiddleware`, `MiddlewareStage`, `MiddlewareOrderAttribute` |
| `Nalix.Abstractions.Microsoft` | Extensions extending standard Microsoft logging infrastructure | `ThrottleLogExtensions` |

## Installation

```bash
dotnet add package Nalix.Abstractions
```

> **Note:** You typically don't need to install this package directly — it is referenced transitively by all higher-level Nalix packages.

## Documentation

For detailed API reference, see the [Nalix.Abstractions package guide](https://ppn-system.me/api/Abstractions/index).
