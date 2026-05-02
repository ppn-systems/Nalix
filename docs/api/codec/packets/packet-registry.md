# Packet Registry

This page covers packet discovery and registry APIs in `Nalix.Framework.DataFrames`.

## Source mapping

- `src/Nalix.Codec/DataFrames/PacketRegistryFactory.cs`
- `src/Nalix.Codec/DataFrames/PacketRegistry.cs`

## Main types

- `PacketRegistryFactory`
- `PacketRegistry`

## Public members at a glance

| Type | Public members |
| --- | --- |
| `PacketRegistryFactory` | `RegisterPacket`, `RegisterAllPackets`, `RegisterPacketAssembly`, `IncludeAssembly(Assembly)`, `IncludeAssembly(string)`, `IncludeCurrentDomain`, `RegisterCurrentDomainPackets`, `IncludeNamespace`, `IncludeNamespaceRecursive`, `CreateCatalog`, `Compute` |
| `PacketRegistry` | `Configure(IObjectPoolManager)`, `LoadFromNamespace(...)`, `LoadFromAssemblyPath(...)`, `IsKnownMagic`, `IsRegistered`, `Deserialize`, `TryDeserialize`, `DeserializerCount` |

## PacketRegistryFactory

`PacketRegistryFactory` is the fluent builder for an immutable `PacketRegistry`.

The constructor pre-registers built-in signal packets:

- `Control`
- `Handshake`
- `SessionResume`
- `Directive`

### Common workflows

- Register explicit packet types.
- Register all packet types from an `Assembly`.
- Register all packet types from a `.dll` file path.
- Scan loaded assemblies, then filter by namespace.

### Basic usage

```csharp
PacketRegistryFactory factory = new();

factory.IncludeCurrentDomain()
       .IncludeNamespaceRecursive("MyApp.Packets");

PacketRegistry registry = factory.CreateCatalog();
```

### Assembly path usage

```csharp
PacketRegistryFactory factory = new();
factory.RegisterPacketAssembly(@"C:\apps\MyPackets.dll", requireAttribute: true);

PacketRegistry registry = factory.CreateCatalog();
```

## PacketRegistry

`PacketRegistry` is the runtime, immutable, thread-safe lookup catalog.

### What it provides

- Fast magic-number lookup.
- Packet-type registration checks.
- Deserialization from raw packet bytes.

### Runtime use

```csharp
if (registry.TryDeserialize(buffer, out IPacket? packet))
{
    Console.WriteLine(packet.Header.OpCode);
}
```

### Static convenience loaders

- `LoadFromAssemblyPath(assemblyPath, requirePacketAttribute)`
- `LoadFromNamespace(packetNamespace, recursive)`
- `LoadFromNamespace(assemblyPath, packetNamespace, recursive)`

## Practical notes

- Use one shared registry instance across server runtime and client SDK whenever possible.
- Prefer `ConfigurePacketRegistry(...)` in hosting if you already have a pre-built registry.
- Prefer namespace filters when you want tighter discovery boundaries than full assembly scans.

## Related APIs

- [Frame Model](./frame-model.md)
- [Built-in Frames](./built-in-frames.md)
- [Packet Contracts](../../abstractions/packet-contracts.md)
- [SDK Overview](../../sdk/index.md)
- [Packet Dispatch](../../runtime/routing/packet-dispatch.md)
- [Network Application (Hosting)](../../hosting/network-application.md)
