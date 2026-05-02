# Dispatch Contracts

This page covers the public dispatch contracts used by `Nalix.Runtime` routing components.

## Audit Summary

- Existing source mapping pointed to `Nalix.Network` paths for contracts that are defined in `Nalix.Runtime`.
- Contract responsibilities were described at a high level but did not call out operational boundaries.

## Missing Content Identified

- Clear separation between queue contract (`IDispatchChannel<TPacket>`) and runtime entry contract (`IPacketDispatch`).
- Practical guidance on when each contract should be implemented directly.

## Improvement Rationale

Accurate source mapping and boundary-focused descriptions reduce integration errors when extending dispatch behavior.

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
