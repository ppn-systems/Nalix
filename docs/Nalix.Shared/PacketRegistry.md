# PacketRegistry — High-performance Packet Deserializer & Transformer Registry

The `PacketRegistry` is a thread-safe, immutable catalog that maps packet type information to fast deserializers and transformers.  
It enables zero-allocation, version-safe decoding and optional transformation for custom networking packets in high-throughput systems.

- **Namespace:** `Nalix.Shared.Registry`
- **Classes:** `PacketRegistry`, `PacketRegistryFactory`
- **Recommended for:** Custom networking protocols, binary protocol gateways, packet router/mux/demux.

---

## Features

- **Immutable & thread-safe** — safe for concurrent access & static usage
- **Maps 32-bit magic numbers** to deserializer delegates (`TryDeserialize`)
- **Fast lookups by type or magic number** (supports millions of packets/sec)
- **Customizable packet registration:** add types, assemblies, scan domains
- **Optional transformations:** compression, encryption, custom processing

---

## Quick Usage Example

### 1. Build a registry for your packets

```csharp
using Nalix.Shared.Registry;
using MyCompany.Packets; // Your concrete packet types

var registry = new PacketRegistry(factory => {
    factory.RegisterPacket<MyDataPacket>();
    // You can also include all packets from an assembly:
    factory.IncludeAssembly(typeof(MyDataPacket).Assembly);
    // Add more packets/assemblies as needed
});
```

---

### 2. Deserialize an incoming packet

```csharp
ReadOnlySpan<byte> buffer = ...; // Incoming packet bytes

if (registry.TryDeserialize(buffer, out var packet))
{
    // 'packet' is an IPacket instance: handle, route, or dispatch
    Handle(packet);
}
else
{
    // Unknown type, unsupported packet, or malformed
}
```

---

### 3. (Optional) Transform a packet

You can look up registered transformers to compress, decompress, encrypt, or decrypt:

```csharp
if (registry.TryGetTransformer(packet.GetType(), out var transformer))
{
    var compressed = transformer.Compress?.Invoke(packet);
    var encrypted = transformer.Encrypt?.Invoke(packet, keyBytes, cipherType);
}
```

**Note:** All delegates are optional; null if not supported by the packet type.

---

## API Overview

### PacketRegistry

| Method                                                                        | Purpose                                               |
|-------------------------------------------------------------------------------|-------------------------------------------------------|
| `.TryDeserialize(ReadOnlySpan<byte> data, out IPacket? packet)`               | Deserialize packet from bytes using magic number      |
| `.TryGetDeserializer(uint magic, out PacketDeserializer? dser)`               | Lookup deserializer by magic number                   |
| `.TryGetTransformer(Type type, out PacketTransformer transformer)`            | Lookup transformer (compression/encryption) by type   |

**Note:** Magic number = `uint` identifier uniquely mapped per packet type.

---

### PacketRegistryFactory

Builds and configures a `PacketRegistry`.

| Method                       | Purpose                                                      |
|------------------------------|--------------------------------------------------------------|
| `RegisterPacket<TPacket>()`  | Explicitly add a packet type                                 |
| `IncludeAssembly(asm)`       | Scan all suitable types in an assembly                       |
| `CreateCatalog()`            | Build the immutable PacketRegistry from the factory          |

---

## Default Packet Types

- The factory automatically registers built-in packets common for text/control use (`Text256`, `Text512`, `Control`, etc.).
- **Supported requirements:**
  - Class implements `IPacket`
  - Static methods for deserialization & (optional) transformation

---

## Implementation Note

- **Immutability:** Once built, the registry cannot be changed.  
- **Concurrency:** All lookups are thread-safe and allocation-free.
- **Custom Transformation:** Compression, decompression, encryption, and decryption are supported if implemented on your packet type, but optional.

---

## Best Practices

- Register all custom packet types you plan to use.
- For plug-and-play: include assemblies/modules so new packets are automatically recognized.
- For optimal performance and correctness, ensure magic numbers are deterministic (do **not** change packet type or namespace between versions).

---

## Example: Full Registration and Handler Setup

```csharp
var registry = new PacketRegistry(factory => {
    factory.RegisterPacket<LoginPacket>()
           .RegisterPacket<ChatMessagePacket>()
           .IncludeAssembly(typeof(LoginPacket).Assembly);
});

if (registry.TryDeserialize(incomingBuffer, out var packet))
{
    RoutePacket(packet);
}
```

---

## Error Handling

- Will return `false` if the magic number is unknown or the buffer is too short.
- Transformation delegates (compression, encryption, etc.) are null if the feature is not available for the packet type.

---

## License

Licensed under the Apache License, Version 2.0.
