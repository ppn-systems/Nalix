# Packet Registry

This page covers the packet catalog APIs in `Nalix.Framework.DataFrames`.

## Source mapping

- `src/Nalix.Framework/DataFrames/PacketRegistryFactory.cs`
- `src/Nalix.Framework/DataFrames/PacketRegistry.cs`

## Main types

- `PacketRegistryFactory`
- `PacketRegistry`

## PacketRegistryFactory

`PacketRegistryFactory` builds an immutable `PacketRegistry` by registering packet types explicitly or by scanning assemblies and namespaces.

## Basic usage

```csharp
PacketRegistryFactory factory = new();

factory.IncludeCurrentDomain()
       .IncludeNamespaceRecursive("MyApp.Packets");

PacketRegistry registry = factory.CreateCatalog();
```

### Common public methods

- `RegisterPacket<TPacket>()`
- `IncludeAssembly(assembly)`
- `IncludeCurrentDomain()`
- `IncludeNamespace(namespace)`
- `IncludeNamespaceRecursive(namespace)`
- `CreateCatalog()`
- `Compute(type)`

## PacketRegistry

`PacketRegistry` is the immutable runtime catalog used by listeners and SDK sessions.

## Basic usage

```csharp
if (registry.TryDeserialize(buffer, out IPacket? packet))
{
    Console.WriteLine(packet.OpCode);
}

bool known = registry.IsKnownMagic(magic);
bool registered = registry.IsRegistered<Handshake>();
```

### Common public methods

- `IsKnownMagic(magic)`
- `IsRegistered<TPacket>()`
- `TryDeserialize(raw, out packet)`
- `TryGetDeserializer(magic, out deserializer)`
- `DeserializerCount`

## Related APIs

- [Frame Model](./frame-model.md)
- [Built-in Frames](./built-in-frames.md)
- [Packet Contracts](../../common/packet-contracts.md)
- [SDK Overview](../../sdk/index.md)
- [Packet Dispatch](../../routing/packet-dispatch.md)
