# Built-in Frames

This page covers the built-in packet types that Nalix ships out of the box.

## Source mapping

- `src/Nalix.Codec/DataFrames/SignalFrames/Control.cs`
- `src/Nalix.Codec/DataFrames/SignalFrames/Handshake.cs`
- `src/Nalix.Codec/DataFrames/SignalFrames/SessionResume.cs`
- `src/Nalix.Codec/DataFrames/SignalFrames/Directive.cs`

## Main types

- `Control`
- `Handshake`
- `SessionResume`
- `Directive`

## Control

`Control` is the built-in frame for protocol control traffic such as ping/pong and related signaling.

## Basic usage

```csharp
var control = new Control();
control.Initialize(ControlType.PING, sequenceId: 42, flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);
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
    HandshakeStage.CLIENT_HELLO,
    clientPublicKey,
    clientNonce,
    flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);

handshake.UpdateTranscriptHash(transcriptBytes);
```

Important public members:

- constructor `(stage, publicKey, nonce, proof, flags)`
- `Initialize(stage, publicKey, nonce, proof, flags)`
- `InitializeError(ProtocolReason, PacketFlags)`
- `Validate(out string?)`
- `ComputeTranscriptHash(...)`
- `UpdateTranscriptHash(...)`
- `ResetForPool()`
- `DynamicSize`

## Packet pooling

Packet instances are pooled through type-specific helpers:

- `PacketFactory<TPacket>` — rents a packet wrapped in `PacketScope<TPacket>`
- `PacketScope<TPacket>` — zero-allocation scope that returns the packet on dispose

Use the scope-based API when you want the packet to return itself to the pool automatically.

## Related APIs

- [Frame Model](./frame-model.md)
- [Packet Registry](./packet-registry.md)
- [Packet Pooling](../../runtime/pooling/packet-pooling.md)
- [Session Extensions](../../sdk/tcp-session-extensions.md)
- [Handler Return Types](../../runtime/routing/handler-results.md)
