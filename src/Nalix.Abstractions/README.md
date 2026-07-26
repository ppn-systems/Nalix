# Nalix.Abstractions

Foundational contracts, enums, attributes, and shared primitives for the Nalix stack.

Nalix.Abstractions is the dependency floor for the repository. Higher-level packages build on
these interfaces instead of sharing implementation details across package boundaries.

## Install

```bash
dotnet add package Nalix.Abstractions
```

Most applications do not need to reference this package directly. It is pulled in transitively by
Nalix.Codec, Nalix.Network, Nalix.Runtime, Nalix.Hosting, and Nalix.SDK.

## What It Provides

| Area | Purpose | Main types |
| :--- | :--- | :--- |
| Lifecycle and pooling | Shared lifecycle and pool reset contracts | `IPoolable`, `IActivatable`, `IBufferLease`, `IBufferPoolManager`, `IObjectPoolManager` |
| Connections | Transport-neutral connection contracts | `IConnection`, `IConnectionHub`, `INetworkEndpoint`, `NetworkTransport` |
| Packets | Packet metadata, context, and sending abstractions | `IPacket`, `IPacketContext`, `IPacketSender`, `PacketOpcodeAttribute`, `PacketTransportAttribute` |
| Protocols | Wire-level control codes and protocol advice | `ProtocolOpCode`, `ProtocolAdvice`, `ControlFlags`, `ProtocolReason` |
| Sessions | Session persistence contracts | `ISessionStore`, `ISessionService`, `ISessionFactory`, `SessionEntry` |
| Serialization | Source-generator serialization metadata | `SerializeOrderAttribute`, `SerializeHeaderAttribute`, `SerializeLayout`, `IFixedSizeSerializable` |
| Security | Cipher, permission, and sequence contracts | `CipherSuiteType`, `PermissionLevel`, `DropPolicy`, `ISequenceCounter` |
| Concurrency | Worker and task scheduling contracts | `ITaskManager`, `IWorker`, `IWorkerHandle`, `WorkerPriority` |
| Middleware | Packet pipeline extension points | `IPacketMiddleware`, `MiddlewareStage`, `MiddlewareOrderAttribute` |

## Package Role

Use Nalix.Abstractions when you are writing reusable components that must compile without taking a
dependency on Nalix.Network, Nalix.Runtime, or other implementation packages.

## Documentation

- Package guide: https://ppn.io.vn/packages/nalix-abstractions/
- API reference: https://ppn.io.vn/api/abstractions/
