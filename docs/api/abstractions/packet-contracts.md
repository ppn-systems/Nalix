# Packet Contracts

`Nalix.Abstractions.Networking.Packets` defines the shared packet contracts used by both server and client packages.

## Why These Contracts Exist

Nalix uses one packet model across runtime and SDK code. Shared contracts prevent divergence between transport, dispatch, and application handlers.

## Source Mapping

- `src/Nalix.Abstractions/Networking/Packets/IPacket.cs`
- `src/Nalix.Abstractions/Networking/Packets/IPacketContext.cs`
- `src/Nalix.Abstractions/Networking/Packets/IPacketDeserializer.cs`
- `src/Nalix.Abstractions/Networking/Packets/IPacketRegistry.cs`
- `src/Nalix.Abstractions/Networking/Packets/IPacketSender.cs`
- `src/Nalix.Abstractions/Networking/Packets/PacketDeserializer.cs`
- `src/Nalix.Abstractions/Networking/Packets/IPacketTimestamped.cs`
- `src/Nalix.Abstractions/Networking/Packets/IPacketReasoned.cs`

## Main Types

### `IPacket`

`IPacket` is the wire contract. It includes:

- header metadata (`MagicNumber`, `OpCode`, `Flags`, `Priority`, `SequenceId`)
- `Length`
- serialization methods (`Serialize()`, `Serialize(Span<byte>)`)

### `IPacketRegistry`

`IPacketRegistry` provides read-only deserializer lookup for dispatch and client receive paths:

- `DeserializerCount`
- `IsKnownMagic(uint)`
- `IsRegistered<TPacket>()`
- `Deserialize(ReadOnlySpan<byte>)`
- `TryDeserialize(ReadOnlySpan<byte>, out IPacket?)`

### `IPacketContext<TPacket>`

Handler context contract shared with runtime context implementations:

- `Packet`, `Connection`, `Attributes`, `Sender`, `CancellationToken`
- `SkipOutbound` for outbound middleware control

### `IPacketSender<TPacket>`

Metadata-aware send contract:

- `SendAsync(TPacket, CancellationToken)`
- `SendAsync(TPacket, bool forceEncrypt, CancellationToken)`

### Supporting Contracts

- `PacketDeserializer`: delegate from raw bytes to `IPacket`
- `IPacketDeserializer<TPacket>`:
  - `Deserialize(ReadOnlySpan<byte>)`
  - `Deserialize(ReadOnlySpan<byte>, ref TPacket)`
- `IPacketTimestamped`: packet contract with timestamp semantics
- `IPacketReasoned`: packet contract exposing reason/code semantics

## Responsibility Boundaries

- `Nalix.Abstractions`: only contracts and shared primitives.
- `Nalix.Framework`: concrete packet model and registry implementations.
- `Nalix.Runtime`: dispatch/context/sender implementations.
- `Nalix.SDK`: client transport usage of the same contracts.

## Best Practices

- Keep packet serialization deterministic for registry deserialization.
- Use `IPacketContext<TPacket>.Sender` in handlers to preserve metadata-driven send behavior.
- Use `TryDeserialize` in hot paths where exception-free failure handling is preferred.

## Related APIs

- [Frame Model](../codec/packets/frame-model.md)
- [Packet Registry](../codec/packets/packet-registry.md)
- [Runtime Packet Context](../runtime/routing/packet-context.md)
- [SDK Overview](../sdk/index.md)
