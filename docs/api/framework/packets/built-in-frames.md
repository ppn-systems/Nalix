# Built-in Frames

This page covers the built-in packet types that Nalix ships out of the box.

## Source mapping

- `src/Nalix.Framework/DataFrames/SignalFrames/Control.cs`
- `src/Nalix.Framework/DataFrames/SignalFrames/Handshake.cs`

## Main types

- `Control`
- `Handshake`
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


## Handshake

`Handshake` is the default key-exchange frame. It carries a handshake `Stage`, ephemeral `PublicKey`, `Nonce`, optional `Proof`, and a `TranscriptHash` derived with `Keccak-256`.

## Basic usage

```csharp
var handshake = new Handshake(
    1,
    HandshakeStage.CLIENT_HELLO,
    clientPublicKey,
    clientNonce,
    transport: ProtocolType.TCP);

handshake.UpdateTranscriptHash(transcriptBytes);
```

Important public members:

- constructor `(opCode, stage, publicKey, nonce, proof, transport)`
- `Initialize(opCode, stage, publicKey, nonce, proof, transport)`
- `ComputeTranscriptHash(...)`
- `UpdateTranscriptHash(...)`
- `ResetForPool()`
- `DynamicSize`


## Packet pooling

Packet instances are pooled through type-specific helpers:

- `PacketPool<TPacket>`
- `PacketLease<TPacket>`

Use the lease-based API when you want the packet to return itself to the pool automatically.

## Related APIs

- [Frame Model](./frame-model.md)
- [Packet Registry](./packet-registry.md)
- [Packet Pooling](./packet-pooling.md)
- [Session Extensions](../../sdk/tcp-session-extensions.md)
- [Handler Return Types](../../runtime/routing/handler-results.md)
