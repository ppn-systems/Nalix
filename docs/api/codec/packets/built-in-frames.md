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

Nalix uses a sophisticated, type-safe pooling system for all packet types. Instead of manual `new` allocations, you should rent packets from the pool to minimize GC pressure.

### The `Create()` Pattern
Every packet inheriting from `PacketBase<TSelf>` provides a static `Create` method:

```csharp
// 1. Rent a packet from the pool
using var handshake = Handshake.Create();

// 2. Initialize the packet
handshake.Initialize(HandshakeStage.CLIENT_HELLO, pubKey, nonce);

// The packet is automatically returned to the pool when 'handshake' is disposed.
```

### Key APIs

- `PacketBase<TSelf>.Create()`: Rents an instance from the underlying `IObjectPoolManager`.
- `PacketBase<TSelf>.Dispose()`: Returns the instance to the pool.
- `PacketRegistry.Manager`: The global pool manager used for all packet types.

## Related APIs

- [Frame Model](./frame-model.md)
- [Packet Registry](./packet-registry.md)
- [Packet Contracts](../../abstractions/packet-contracts.md)
- [Object Pooling](../../framework/memory/object-pooling.md)
