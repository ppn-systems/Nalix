# Packet Pooling

`Nalix.Runtime.Pooling` provides packet-specific pooling helpers for reusable packet instances.

## Source mapping

- `src/Nalix.Runtime/Pooling/PacketFactory.cs`
- `src/Nalix.Runtime/Pooling/PacketScope.cs`

## Main types

- `PacketFactory<TPacket>`
- `PacketScope<TPacket>`

## Public members at a glance

| Type | Public members |
|---|---|
| `PacketScope<TPacket>` | `Value`, `IsValid`, `Dispose()` |
| `PacketFactory<TPacket>` | `Acquire()` |

## PacketScope<`TPacket`>

`PacketScope<TPacket>` is a zero-allocation readonly struct that ensures a rented packet is returned to its pool upon disposal.

Disposing the scope returns the packet to its originating pool.

That makes it a good fit when you want a short-lived packet object without manually returning it yourself.

### Practical notes

- the scope wraps exactly one packet instance
- the packet is returned when the scope is disposed
- use `using` or `using var` so the pool release happens even if later code throws

## PacketFactory<`TPacket`>

`PacketFactory<TPacket>` is the static pool API for a specific packet type.

It provides:

- `Acquire()` — rents a packet and wraps it in a `PacketScope<TPacket>`

### Practical notes

- `Acquire()` is the safest option — the returned scope automatically returns the packet on dispose
- for startup warm-up or pool reset, use `ObjectPoolManager.Prealloc<T>()` and `ObjectPoolManager.ClearPool<T>()` directly

### Common pitfalls

- forgetting to dispose a scope
- using a packet reference after its scope has been disposed
- keeping a pooled packet reference alive after it was returned

## Basic usage

```csharp
using PacketScope<Control> scope = PacketFactory<Control>.Acquire();
Control packet = scope.Value;
packet.Type = ControlType.PING;
```

## When to use which

| Need | Start with |
| --- | --- |
| Automatic return to the pool | `PacketScope<TPacket>` via `PacketFactory<T>.Acquire()` |
| Startup warm-up | `ObjectPoolManager.Prealloc<T>(count)` |
| Pool reset or teardown | `ObjectPoolManager.ClearPool<T>()` |

## Related APIs

- [Frame Model](../../codec/packets/frame-model.md)
- [Built-in Frames](../../codec/packets/built-in-frames.md)
- [Packet Registry](../../codec/packets/packet-registry.md)
