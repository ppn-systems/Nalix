# Nalix.Abstractions

## Role

Lowest-level shared dependency in the Nalix ecosystem. Defines all cross-cutting contracts, interfaces, attributes, enums, and primitive types consumed by every other project.

**Dependencies:** None (zero internal project references). Only external: `Microsoft.Extensions.Logging.Abstractions`.

## Directory Structure

```
Nalix.Abstractions/
├── Concurrency/         # ITaskManager, IWorkerHandle, IRecurringHandle, WorkerPriority
├── Exceptions/          # BaseException, CipherException, NetworkException, SerializationFailureException, etc.
├── Identity/            # ISnowflake, SnowflakeType
├── Microsoft/           # ThrottleLogExtensions (polyfills for Microsoft.Extensions)
├── Middleware/           # IPacketMiddleware, MiddlewareOrderAttribute, MiddlewareStage
├── Networking/           # IConnection, IConnectionHub, IProtocol, IListener, INetworkEndpoint, ITransportSequencer
│   ├── Packets/         # IPacket, PacketBase contracts, opcode definitions
│   ├── Protocols/       # Protocol-level enums and contracts
│   └── Sessions/        # Session management abstractions
├── Primitives/          # Bytes32 (fixed-size value type), PacketHeader
├── Security/            # CipherSuiteType, DropPolicy, ISequenceCounter, PermissionLevel
├── Serialization/       # Serialization attributes and contracts (see below)
├── IBufferLease.cs      # Buffer pooling contract
├── IBufferPoolManager.cs
├── IObjectPoolManager.cs
├── IPoolable.cs         # Object pool lifecycle
├── IActivatable.cs      # Activation pattern
└── (various attributes) # BorrowedAttribute, SkipCleanAttribute, ConfiguredIgnoreAttribute, etc.
```

## Key Design Rules

- This project MUST NOT depend on any other Nalix project.
- All types here are contracts/abstractions — no implementation logic.
- All public APIs MUST have XML documentation comments.
- Prefer `sealed` classes unless inheritance is explicitly intended.
- Attributes must be lightweight — store only metadata, no behavior.

## Serialization Attributes

The serialization system is fully source-generated. These attributes drive `Nalix.Analyzers.Generators`:

| Attribute | Purpose |
| :--- | :--- |
| `[GenerateFormatter]` | Marks a class for source-generated `IFormatter<T>` |
| `[SerializeOrder(n)]` | Explicit field ordering for deterministic serialization |
| `[SerializeIgnore]` | Exclude a property from serialization |
| `[SerializeHeader]` | Mark header fields (opcode, length) in packet classes |
| `[SerializePackable]` | Mark a type as packable (implements `IFixedSizeSerializable`) |
| `[SerializeDynamicSize]` | Hint for variable-length fields |

## Networking Contracts

`IConnection` is split into partial files:
- `IConnection.cs` — Core identity, state, attributes
- `IConnection.Transmission.cs` — Send/receive methods
- `IConnection.ErrorTracked.cs` — Error tracking
- `IConnection.Hub.cs` — `IConnectionHub` for managing connection collections

## Anti-Patterns

- Do NOT put implementation code here — abstractions only.
- Do NOT add project references to any Nalix project.
- Do NOT use concrete types from other Nalix assemblies.
- Do NOT add runtime-heavy NuGet dependencies.
