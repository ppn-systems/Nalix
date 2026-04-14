# Dispatch Channel and Router

This page documents the lower-level dispatch queue implementation used by runtime dispatchers.

## Audit Summary

- Existing page referenced outdated source paths and a `DispatchRouter<TPacket>` type that is not present in current code.
- Needed alignment with current implementation (`DispatchChannel<TPacket>` only).

## Missing Content Identified

- Correct source mapping to `Nalix.Runtime.Internal.Routing`.
- Accurate boundary between public type visibility and intended internal-runtime use.

## Improvement Rationale

Accurate internals docs prevent contributors from targeting removed or non-existent routing APIs.

## Source Mapping

- `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs`

## `DispatchChannel<TPacket>`

`DispatchChannel<TPacket>` is a priority-aware, connection-associated queue implementation used under `PacketDispatchChannel`.

### Why it exists

Dispatch runtime needs efficient enqueue/dequeue behavior with per-connection isolation and priority selection while remaining compatible with `IDispatchChannel<TPacket>`.

### Publicly visible members

- `TotalPackets`
- `HasPacket`
- `Push(IConnection, IBufferLease)`
- `Pull(out IConnection, out IBufferLease)`

### Internal diagnostics members (not part of `IDispatchChannel<TPacket>`)

- `TotalConnections`
- `ReadyConnections`
- `PendingPerPriority`
- `PendingPerConnection`
- `PushCore(IConnection, IBufferLease)`

## Architecture Notes

- Maintains per-connection state with per-priority queues.
- Pull path prefers higher priority first.
- Enqueue path uses `DispatchOptions` and drop policy behavior.
- Integrates with `IConnectionHub.ConnectionUnregistered` for state cleanup.

## Related APIs

- [Dispatch Contracts](./dispatch-contracts.md)
- [Packet Dispatch](./packet-dispatch.md)
- [Dispatch Options](../options/dispatch-options.md)
