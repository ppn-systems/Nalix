# Nalix.Common — Shared Contracts and Abstractions

**Nalix.Common** is a contracts-only library: interfaces, enums, attributes, and protocol types used across Nalix.Network, Nalix.SDK, Nalix.Logging, and other modules. It contains no runtime implementation; it defines the shape of packets, connections, middleware, and diagnostics.

---

## Overview

| Area | Contents |
|------|----------|
| **Networking** | `IConnection`, `ITcp`, `IUdp`, `IConnectEventArgs`, `INetworkEndpoint`, `IConnectionHub`, `IConnectionErrorTracked` |
| **Packets** | `IPacket`, `IPacketSender`, `IPacketRegistry`, `IPacketDeserializer`, `IPacketCompressor`, packet attributes (`PacketOpcode`, `PacketController`, `PacketEncryption`, etc.) |
| **Protocol** | `ControlType`, `ControlFlags`, `ProtocolReason`, `ProtocolAdvice`, `ProtocolType` |
| **Middleware** | `MiddlewareStage`, `[MiddlewareOrder]`, `[MiddlewareStage]`, `[PipelineManagedTransform]` |
| **Serialization** | `SerializePackable`, `SerializeOrder`, `SerializeIgnore`, `SerializeDynamicSize`, layout and bounds |
| **Diagnostics** | `ILogger`, `ILoggerFormatter`, `ILoggerTarget`, `ILogDistributor`, log levels, event IDs |
| **Identity / Security** | `ISnowflake`, `SnowflakeType`, `CipherSuiteType`, `PermissionLevel`, `DataSensitivityLevel` |
| **Concurrency** | `ITaskManager`, `IRecurringHandle`, `IRecurringOptions`, `IWorkerContext` |

---

## Who Uses It

- **Nalix.Network** implements `IConnection` (Connection), uses packet attributes and protocol enums, compiles handlers from `[PacketController]` / `[PacketOpcode]`.
- **Nalix.SDK** uses `IClientConnection`, packet and protocol types for client transport.
- **Nalix.Logging** implements `ILogger` and related diagnostics interfaces.

---

## See Also

- [Nalix.Network Architecture](../Nalix.Network/Architecture.md)
- [Packet Attributes](../Nalix.Network/Routing/PacketAttributes.md)
- [Middleware Attributes](../Nalix.Network/Middleware/Attributes.md)
