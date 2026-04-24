# Abstractions API

The `Nalix.Abstractions` package defines the fundamental interfaces, attributes, and data structures that are shared across the entire Nalix stack, including both the server-side runtime and the client-side SDK.

## Core Contracts

- [**Packet Contracts**](./packet-contracts.md) — `IPacket`, `PacketOpcodeAttribute`, and basic framing.
- [**Connection Contracts**](./connection-contracts.md) — `IConnection`, `IConnectionHub`, and state tracking.
- [**Session Contracts**](./session-contracts.md) — `ISession`, `ISessionStore`, and resumption tokens.
- [**Concurrency Contracts**](./concurrency-contracts.md) — `IConcurrencyGate` and throttling interfaces.

## High-Level Primitives

- [**Control Types**](./protocols/control-type.md) — Enums for system-level signaling.
- [**Common Enumerations**](./enums.md) — Reference table for all system-wide Enums.
- [**Serialization Attributes**](./serialization-attributes.md) — Metadata for the binary serializer.

