# Dispatch Channel and Router

This page covers the lower-level queueing primitives used by the network dispatch runtime.

## Source mapping

- `src/Nalix.Network/Routing/Channel/DispatchChannel.cs`
- `src/Nalix.Network/Routing/Channel/DispatchRouter.cs`

## Main types

- `DispatchChannel<TPacket>`
- `DispatchRouter<TPacket>`

## DispatchChannel<TPacket>

`DispatchChannel<TPacket>` is the per-connection queueing layer used to stage packet leases before handler execution.

It keeps:

- one queue per connection
- one ready queue per priority
- per-connection counters
- drop-policy handling through `DispatchOptions`

## Basic usage

```csharp
channel.Push(connection, lease);

if (channel.Pull(out IConnection nextConnection, out IBufferLease nextLease))
{
    // pass to the next processing stage
}
```

### Public members

- `TotalPackets`
- `HasPacket`
- `Push(connection, lease)`
- `Pull(out connection, out lease)`

## DispatchRouter<TPacket>

`DispatchRouter<TPacket>` shards multiple `DispatchChannel<TPacket>` instances and spreads work by connection hash.

Use it when one channel is not enough and you want parallel shard routing without changing the higher-level queue contract.

## Example

```csharp
var router = new DispatchRouter<IPacket>(shardCount: 8);

router.Push(connection, lease);

if (router.Pull(out IConnection nextConnection, out IBufferLease nextLease))
{
    // consume from the selected shard
}
```

### Public members

- constructor `(shardCount)`
- `TotalPackets`
- `Push(connection, lease)`
- `Pull(out connection, out lease)`

## Related APIs

- [Dispatch Contracts](./dispatch-contracts.md)
- [Packet Dispatch](./packet-dispatch.md)
- [Dispatch Options](../options/dispatch-options.md)
