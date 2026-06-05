# Built-in Frames

This page covers the built-in packet types that Nalix ships out of the box.

## Source mapping

- `src/Nalix.Codec/ProtocolFrames/Control.cs`
- `src/Nalix.Codec/ProtocolFrames/Directive.cs`
- `src/Nalix.Codec/ProtocolFrames/TimeSync.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionInit.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionProof.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionTofu.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionResume.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionChallenge.cs`
- `src/Nalix.Codec/ProtocolFrames/Session/SessionEstablished.cs`

## Main types

- `Control`
- `Directive`
- `TimeSync`
- `SessionInit`
- `SessionProof`
- `SessionTofu`
- `SessionResume`
- `SessionChallenge`
- `SessionEstablished`

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

## SessionInit

`SessionInit` is the first step of the session handshake where the client sends its ephemeral public key and nonce.

```csharp
var init = SessionInit.Create();
init.Initialize(clientPublicKey, clientNonce, PacketFlags.SYSTEM | PacketFlags.RELIABLE);
```

Important public members:

- `PublicKey` (`Bytes32`) — the client's ephemeral public key
- `Nonce` (`Bytes32`) — the client's nonce
- `Initialize(Bytes32 publicKey, Bytes32 nonce, PacketFlags flags)`
- `Validate(out string?)`
- `ResetForPool()`

## SessionProof

`SessionProof` is the client's confirmation of the derived transcript and proof of possession.

```csharp
var proof = SessionProof.Create();
proof.Initialize(clientProof, PacketFlags.SYSTEM | PacketFlags.RELIABLE);
```

Important public members:

- `Proof` (`Bytes32`) — the client's proof
- `Initialize(Bytes32 proof, PacketFlags flags)`
- `Validate(out string?)`
- `ResetForPool()`

## SessionTofu

`SessionTofu` is a Trust-On-First-Use packet where the server returns its static public key.

```csharp
var tofu = SessionTofu.Create();
tofu.Initialize(serverPublicKey);
```

Important public members:

- `PublicKey` (`Bytes32`) — the server's public key
- `Initialize(Bytes32 publicKey)`
- `Validate(out string?)`
- `ResetForPool()`

## SessionResume

`SessionResume` is a fixed-size frame for resuming a previously established session using a token and proof.

Important public members:

- `Stage` (`SessionResumeStage`) — `NONE`, `REQUEST`, or `RESPONSE`
- `SessionToken` (`ulong`) — the session token
- `Reason` (`ProtocolReason`) — reason code (used in responses)
- `Proof` (`Bytes32`) — HMAC proof of session secret possession
- `Initialize(SessionResumeStage, ulong, ProtocolReason, Bytes32, PacketFlags)`
- `Validate(out string?)`
- `ResetForPool()`

## SessionChallenge

`SessionChallenge` is the server's response to `SessionInit`, providing its ephemeral key, nonce, and proof.

Important public members:

- `PublicKey` (`Bytes32`) — the server's ephemeral public key
- `Nonce` (`Bytes32`) — the server's nonce
- `Proof` (`Bytes32`) — the server's challenge proof
- `Initialize(Bytes32 publicKey, Bytes32 nonce, Bytes32 proof, PacketFlags flags)`
- `Validate(out string?)`
- `ResetForPool()`

## SessionEstablished

`SessionEstablished` is the server's acknowledgment of handshake completion and session establishment.

Important public members:

- `Proof` (`Bytes32`) — the server's final finish proof
- `SessionToken` (`ulong`) — the assigned session token
- `Initialize(Bytes32 proof, ulong sessionToken, PacketFlags flags)`
- `Validate(out string?)`
- `ResetForPool()`

## Packet pooling

Nalix uses a sophisticated, type-safe pooling system for all packet types. Instead of manual `new` allocations, you should rent packets from the pool to minimize GC pressure.

### The `Create()` Pattern
Every packet inheriting from `PacketBase<TSelf>` provides a static `Create` method:

```csharp
// 1. Rent a packet from the pool
using var init = SessionInit.Create();

// 2. Initialize the packet
init.Initialize(clientPublicKey, clientNonce);

// The packet is automatically returned to the pool when 'init' is disposed.
```

### Key APIs

- `PacketBase<TSelf>.Create()`: Rents an instance from the underlying `IObjectPoolManager`.
- `PacketBase<TSelf>.Dispose()`: Returns the instance to the pool.

## Directive

`Directive` is a directive frame used for control and server feedback. It carries a `ControlType`, `ProtocolReason`, and a `ProtocolAdvice` action.

Important public members:

- `Type` (`ControlType`) — the directive type
- `Reason` (`ProtocolReason`) — the reason for the directive
- `Action` (`ProtocolAdvice`) — the recommended action
- `Initialize(ControlType, ProtocolReason, ProtocolAdvice, ...)`
- `ResetForPool()`

## TimeSync

`TimeSync` is a time synchronization and ping packet used for RTT measurement and clock alignment.

Important public members:

- `Type` (`ControlType`) — the control message type (e.g., `PING`, `PONG`, `TIMESYNCREQUEST`)
- `Timestamp` (`long`) — wall-clock timestamp
- `MonoTicks` (`long`) — monotonic clock ticks
- `Initialize(ControlType, ...)`

## Related APIs

- [Frame Model](./frame-model.md)
- [Packet Registry](./packet-registry.md)
- [Packet Contracts](../../abstractions/packet-contracts.md)
- [Object Pooling](../../framework/memory/object-pooling.md)
