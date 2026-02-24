# Packet Contracts

`Nalix.Common.Networking.Packets` contains the core packet contracts shared by server and client code.
The packet model is generic-friendly, so the same contracts work for built-in packets and custom packet types.

## Source mapping

- `src/Nalix.Common/Networking/Packets/IPacket.cs`
- `src/Nalix.Common/Networking/Packets/IPacketRegistry.cs`
- `src/Nalix.Common/Networking/Packets/IPacketSender.cs`
- `src/Nalix.Common/Networking/Packets/IPacketTimestamped.cs`
- `src/Nalix.Common/Networking/Packets/IPacketReasoned.cs`

## Main types

- `IPacket`
- `IPacketRegistry`
- `IPacketSender<TPacket>`
- `IPacketContext<TPacket>`

## Public members at a glance

| Type | Public members |
|---|---|
| `IPacket` | packet header members, `Length`, `Serialize()` overloads |
| `IPacketRegistry` | registry lookup, registration checks, deserialization helpers |
| `IPacketSender<TPacket>` | packet send helpers with metadata-aware behavior |
| `IPacketContext<TPacket>` | `SkipOutbound`, `Packet`, `Connection`, `Attributes`, `Sender`, `CancellationToken` |

## IPacket

`IPacket` is the base packet contract.

It includes:

- header-level metadata such as magic number, opcode, flags, priority, and protocol
- `Length`
- `Serialize()` overloads

This is the contract that packet implementations on both sides of the wire ultimately conform to.

## IPacketRegistry

`IPacketRegistry` is the read-only packet catalog used to map incoming data to packet deserializers.

It supports:

- checking whether a magic number is known
- checking whether a packet type is registered
- deserializing raw bytes into `IPacket`
- retrieving a deserializer by magic number

## IPacketSender<TPacket>

`IPacketSender<TPacket>` abstracts packet sending with metadata-aware transform behavior.

It supports:

- sending with normal metadata-driven behavior
- sending with an explicit encryption override

## IPacketContext<TPacket>

`IPacketContext<TPacket>` is the handler context used when packet middleware or handlers need the current packet, connection, metadata, and sender together.
Use the generic `TPacket` with built-in packets or your own custom packet types depending on the handler pipeline.

### Common pitfalls

- using `connection.TCP.SendAsync(...)` when `context.Sender` already knows the current packet metadata
- ignoring `SkipOutbound` when a handler intentionally wants to suppress outbound middleware
- treating `Attributes` as optional when your middleware depends on resolved packet metadata

## Example

```csharp
IPacket packet = new Handshake(
    1,
    HandshakeStage.CLIENT_HELLO,
    clientPublicKey,
    clientNonce);
IPacketSender<Handshake> sender = /* resolved sender */;
await sender.SendAsync((Handshake)packet, ct);

if (registry.TryDeserialize(buffer, out IPacket? decoded))
{
    Console.WriteLine($"decoded opcode: {decoded.OpCode}");
}
```

Typical flow:

1. registry resolves raw bytes to a packet
2. handler receives `IPacketContext<TPacket>` or `PacketContext<TPacket>`
3. middleware reads metadata from `context.Attributes`
4. handler sends through `context.Sender` when it needs packet-aware send behavior
5. the same flow works for custom packet handlers as long as the generic packet type matches the dispatch pipeline

## Related APIs

- [Frame Model](../framework/packets/frame-model.md)
- [Packet Registry](../framework/packets/packet-registry.md)
- [Packet Sender](../runtime/routing/packet-sender.md)
- [Packet Dispatch](../runtime/routing/packet-dispatch.md)
- [Packet Metadata](../runtime/routing/packet-metadata.md)
