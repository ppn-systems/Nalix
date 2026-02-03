# Packet Contracts

`Nalix.Common.Networking.Packets` contains the core packet contracts shared by server and client code.

## Source mapping

- `src/Nalix.Common/Networking/Packets/IPacket.cs`
- `src/Nalix.Common/Networking/Packets/IPacketRegistry.cs`
- `src/Nalix.Common/Networking/Packets/IPacketSender.cs`
- `src/Nalix.Common/Networking/Packets/IPacketSequenced.cs`
- `src/Nalix.Common/Networking/Packets/IPacketTimestamped.cs`
- `src/Nalix.Common/Networking/Packets/IPacketReasoned.cs`

## Main types

- `IPacket`
- `IPacketRegistry`
- `IPacketSender<TPacket>`

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

## Example

```csharp
IPacket packet = new Handshake();
IPacketSender<Handshake> sender = /* resolved sender */;
await sender.SendAsync((Handshake)packet, ct);

if (registry.TryDeserialize(buffer, out IPacket? decoded))
{
    Console.WriteLine($"decoded opcode: {decoded.OpCode}");
}
```

## Related APIs

- [Frame Model](../framework/packets/frame-model.md)
- [Packet Registry](../framework/packets/packet-registry.md)
- [Packet Sender](../routing/packet-sender.md)
- [Packet Dispatch](../routing/packet-dispatch.md)
- [Packet Metadata](../routing/packet-metadata.md)
