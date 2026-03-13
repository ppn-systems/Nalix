# Packet Registry

This page covers the packet catalog APIs in `Nalix.Framework.DataFrames`.

## Source mapping

- `src/Nalix.Framework/DataFrames/PacketRegistryFactory.cs`
- `src/Nalix.Framework/DataFrames/PacketRegistry.cs`

## Main types

- `PacketRegistryFactory`
- `PacketRegistry`

## Public members at a glance

| Type | Public members |
|---|---|
| `PacketRegistryFactory` | `RegisterPacket`, `IncludeAssembly`, `IncludeCurrentDomain`, `IncludeNamespace`, `IncludeNamespaceRecursive`, `CreateCatalog`, `Compute` |
| `PacketRegistry` | `IsKnownMagic`, `IsRegistered`, `TryDeserialize`, `TryGetDeserializer`, `DeserializerCount` |

## Source notes

The default factory now pre-registers these built-in packet types:

- `Control`
- `Handshake`

## PacketRegistryFactory

`PacketRegistryFactory` builds an immutable `PacketRegistry` by registering packet types explicitly or by scanning assemblies and namespaces.

It is the object you typically use when you want the same packet catalog on both server and client.

### Typical responsibilities

- register built-in packet types
- scan application assemblies or namespaces
- compute magic numbers from packet types
- build the frozen registry once, then reuse it

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

### Practical notes

- `CreateCatalog()` is the handoff point from builder-style setup to runtime lookup.
- `Compute(type)` is used to derive the magic number for a packet type.
- the factory is the right place to centralize packet registration instead of sprinkling manual registrations across startup files.

### Common pitfalls

- calling `CreateCatalog()` before all packet assemblies are registered
- registering the same packet type in multiple startup paths
- assuming the catalog auto-discovers types without scanning or explicit registration

## PacketRegistry

`PacketRegistry` is the immutable runtime catalog used by listeners and SDK sessions.

Once created, it is intended to be shared and treated as read-only.

### What it gives you

- fast magic-number lookup
- type registration checks
- deserializer resolution
- a single source of truth for packet discovery at runtime

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

- `IsKnownMagic(magic)`: Fast check for registered magic numbers.
- `IsRegistered<TPacket>()`: Checks if a specific packet type is registered.
- `Deserialize(raw)`: Decodes a packet from raw bytes; throws on failure.
- `TryDeserialize(raw, out packet)`: Safely decodes a packet from raw bytes.
- `DeserializerCount`: Gets the total number of registered packet types.

### In-place Deserialization (Zero-allocation)

For mission-critical paths where performance is paramount, `PacketRegistry` supports deserializing data directly **into** an existing packet instance. This is essential for zero-allocation patterns when combined with packet pooling.

```csharp
// Example using an existing instance (e.g., from a pool)
MyPacket reuse = pool.Rent();
if (registry.TryDeserialize(buffer, ref reuse))
{
    // 'reuse' is now populated with data from buffer. 
    // If the buffer contained a different packet type, TryDeserialize returns false.
}
```

#### New In-place Methods
- **`Deserialize<TPacket>(raw, ref value)`**: Deserializes the buffer into the provided `ref value`. Returns the instance. Throws `InvalidOperationException` if the magic number doesn't match `TPacket`.
- **`TryDeserialize<TPacket>(raw, ref value)`**: Attempts to populate `ref value`. Returns `false` if the magic number is unknown, the data is invalid, or the type doesn't match.

### Practical notes

- listeners use the registry while decoding inbound traffic
- SDK sessions use the same registry to stay aligned with the server packet catalog
- if the registry and packet types drift apart, deserialization usually fails fast rather than silently producing the wrong packet

### Common pitfalls

- treating the registry as mutable after startup
- deserializing with a registry built from a different packet set than the sender

## Related APIs

- [Frame Model](./frame-model.md)
- [Built-in Frames](./built-in-frames.md)
- [Packet Contracts](../../common/packet-contracts.md)
- [SDK Overview](../../sdk/index.md)
- [Packet Dispatch](../../runtime/routing/packet-dispatch.md)
