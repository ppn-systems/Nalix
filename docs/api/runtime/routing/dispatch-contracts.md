# Dispatch Contracts

This page covers the public dispatch contracts used by `Nalix.Runtime` routing components.

## Overview

This page covers the public dispatch contracts used by `Nalix.Runtime` routing components. These interfaces define the boundaries between transport, queuing, and execution layers, ensuring that the framework remains modular and extensible.

## Source Mapping

- `src/Nalix.Runtime/Dispatching/IDispatchChannel.cs`
- `src/Nalix.Runtime/Dispatching/IPacketDispatch.cs`
- `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs`

## `IDispatchChannel<TPacket>`

`IDispatchChannel<TPacket>` is the queue abstraction for connection-aware packet scheduling.

### Why it exists

Dispatch runtime needs a consistent contract for enqueue/dequeue behavior without coupling higher layers to a specific queue implementation.

### Key members

- `TotalPackets`
- `Push(IConnection connection, IBufferLease raw)`
- `Pull(out IConnection connection, out IBufferLease raw)`

### When to use

- Use when implementing custom queueing/scheduling internals.
- Most applications should use the provided `DispatchChannel<TPacket>` implementation through `PacketDispatchChannel`.

## `IPacketDispatch`

`IPacketDispatch` is the runtime-facing dispatch entry contract.

### Why it exists

Transport components should forward incoming work to a stable interface regardless of whether data is still raw (`IBufferLease`) or already deserialized (`IPacket`).

### Key members

- `HandlePacket(IBufferLease packet, IConnection connection)`

`IPacketDispatch` also inherits:

- `IActivatable` for lifecycle start/stop
- `IReportable` for diagnostics/reporting

## Practical Example

```csharp
// Raw inbound from transport
packetDispatch.HandlePacket(lease, connection);
```

## Related APIs

- [Packet Dispatch](./packet-dispatch.md)
- [Dispatch Channel and Router](./dispatch-channel-and-router.md)
- [Packet Context](./packet-context.md)
