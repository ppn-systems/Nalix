# Packet Pooling

`Nalix.Framework.DataFrames.Pooling` provides packet-specific pooling helpers for reusable packet instances.

## Source mapping

- `src/Nalix.Framework/DataFrames/Pooling/PacketLease.cs`
- `src/Nalix.Framework/DataFrames/Pooling/PacketPool.cs`

## Main types

- `PacketLease<TPacket>`
- `PacketPool<TPacket>`

## Public members at a glance

| Type | Public members |
|---|---|
| `PacketLease<TPacket>` | `Value`, `Dispose()` |
| `PacketPool<TPacket>` | `Rent`, `Get`, `Return`, `Prealloc`, `Clear` |

## PacketLease<TPacket>

`PacketLease<TPacket>` represents exclusive ownership of a pooled packet instance.

Disposing the lease returns the packet to its originating pool.

That makes it a good fit when you want a short-lived packet object without manually returning it yourself.

### Practical notes

- the lease owns exactly one packet instance
- the packet is returned when the lease is disposed
- use `using` or `using var` so the pool release happens even if later code throws

## PacketPool<TPacket>

`PacketPool<TPacket>` is the static pool API for a specific packet type.

It provides:

- `Rent()`
- `Get()`
- `Return(...)`
- `Prealloc(...)`
- `Clear()`

### Practical notes

- `Rent()` is the safest option when you want automatic return behavior
- `Get()` is useful when you need a raw packet instance and will manage the return path yourself
- `Prealloc(...)` is useful for startup warm-up
- `Clear()` drops pooled instances for that packet type

### Common pitfalls

- forgetting to dispose a lease
- returning a packet manually after it was already leased
- keeping a pooled packet reference alive after it was returned

## Basic usage

```csharp
using PacketLease<Control> lease = PacketPool<Control>.Rent();
Control packet = lease.Value;
packet.Type = ControlType.PING;
```

Or, if you need direct control:

```csharp
Control packet = PacketPool<Control>.Get();
try
{
    packet.Type = ControlType.PONG;
}
finally
{
    PacketPool<Control>.Return(packet);
}
```

## When to use which

| Need | Start with |
|---|---|
| Automatic return to the pool | `PacketLease<TPacket>` |
| Raw packet ownership | `PacketPool<TPacket>.Get()` |
| Startup warm-up | `PacketPool<TPacket>.Prealloc(...)` |
| Pool reset or teardown | `PacketPool<TPacket>.Clear()` |

## Related APIs

- [Frame Model](./frame-model.md)
- [Built-in Frames](./built-in-frames.md)
- [Packet Registry](./packet-registry.md)
