# Dispatch Contracts

This page covers the public routing contracts that sit below `PacketDispatchChannel`.

## Source mapping

- `src/Nalix.Network/Routing/IDispatchChannel.cs`
- `src/Nalix.Network/Routing/IPacketDispatch.cs`

## Main types

- `IDispatchChannel<TPacket>`
- `IPacketDispatch`

## IDispatchChannel<TPacket>

`IDispatchChannel<TPacket>` is the low-level queue contract used by routing internals.
It moves `IBufferLease` instances through the queueing stage before the dispatch runtime turns them into packets.

## Basic usage

```csharp
channel.Push(connection, lease);

if (channel.Pull(out IConnection nextConnection, out IBufferLease nextLease))
{
    // consume work item
}
```

## Public members

- `TotalPackets`
- `Push(connection, raw)`
- `Pull(out connection, out raw)`

## IPacketDispatch

`IPacketDispatch` is the higher-level packet handling contract implemented by dispatchers that can process:

- raw `IBufferLease`
- deserialized `IPacket`

## Example

```csharp
dispatch.HandlePacket(lease, connection);
dispatch.HandlePacket(packet, connection);
```

## Related APIs

- [Packet Dispatch](./packet-dispatch.md)
- [Dispatch Channel and Router](./dispatch-channel-and-router.md)
