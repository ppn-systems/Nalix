# Built-in Frames

This page covers the built-in packet types that Nalix ships out of the box.

## Source mapping

- `src/Nalix.Framework/DataFrames/SignalFrames/Control.cs`
- `src/Nalix.Framework/DataFrames/SignalFrames/Directive.cs`
- `src/Nalix.Framework/DataFrames/SignalFrames/Handshake.cs`
- `src/Nalix.Framework/DataFrames/TextFrames/Text256.cs`
- `src/Nalix.Framework/DataFrames/TextFrames/Text512.cs`
- `src/Nalix.Framework/DataFrames/TextFrames/Text1024.cs`

## Main types

- `Control`
- `Directive`
- `Handshake`
- `Text256`
- `Text512`
- `Text1024`

## Control

`Control` is the built-in frame for protocol control traffic such as ping/pong and related signaling.

## Basic usage

```csharp
var control = new Control();
control.Initialize(ControlType.PING, sequenceId: 42, transport: ProtocolType.TCP);
```

Important public members:

- `Initialize(ControlType, ...)`
- `Initialize(opCode, ControlType, ...)`
- `ResetForPool()`

## Directive

`Directive` is the server-to-client control frame for throttle, redirect, notice, and nack flows.

## Basic usage

```csharp
var directive = new Directive();
directive.Initialize(
    ControlType.THROTTLE,
    ProtocolReason.BUSY,
    ProtocolAdvice.RETRY,
    sequenceId: 42,
    arg0: 500);
```

Important public members:

- `Initialize(ControlType, ...)`
- `Initialize(opCode, ControlType, ...)`

## Handshake

`Handshake` carries binary handshake payloads such as public keys.

## Basic usage

```csharp
var handshake = new Handshake(1, clientPublicKey, ProtocolType.TCP);
handshake.Initialize(clientPublicKey, ProtocolType.TCP);
```

Important public members:

- constructor `(opCode, data, transport)`
- `Initialize(data, transport)`
- `ResetForPool()`
- `DynamicSize`

## Text frames

`Text256`, `Text512`, and `Text1024` are the built-in text packet types used when the dispatcher needs to emit string payloads.

## Packet pooling

Packet instances are pooled through type-specific helpers:

- `PacketPool<TPacket>`
- `PacketLease<TPacket>`

Use the lease-based API when you want the packet to return itself to the pool automatically.

## Related APIs

- [Frame Model](./frame-model.md)
- [Packet Registry](./packet-registry.md)
- [Packet Pooling](./packet-pooling.md)
- [SDK Extensions](../../sdk/tcp-session-extensions.md)
- [Handler Return Types](../../routing/handler-results.md)
