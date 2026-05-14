# Packet Registry

This page covers packet discovery and registry APIs in `Nalix.Codec.DataFrames`.

## Source mapping

- `src/Nalix.Codec/DataFrames/PacketRegistry.cs`

## Core Concepts

In Nalix, the `PacketRegistry` is a process-wide, immutable, thread-safe catalog of packet types and their associated deserializers. It is optimized for ultra-fast lookup via the packet's **Magic Number** (auto-generated hash of the type name).

### 1. Automatic Discovery (Source Generation)

Nalix uses a C# Source Generator to automatically detect types inheriting from `PacketBase<TSelf>` or implementing `IPacket`. The generator produces calls to `PacketRegistry.RegisterGenerated(...)` which are executed during assembly initialization.

### 2. Frozen Catalog

The registry must be "built" before it can be used for deserialization. Calling `PacketRegistry.Build()` freezes the internal catalog, making it read-only and thread-safe for high-performance dispatch.

## Main types

- `PacketRegistry`

## Public members at a glance

| Type | Public members |
| --- | --- |
| `PacketRegistry` | `Configure(IObjectPoolManager)`, `RegisterGenerated(PacketDispatch)`, `RegisterGenerated(uint, string, PacketDeserializer)`, `Build()`, `IsKnownMagic`, `TryDeserialize`, `Manager` |

## Usage

### 1. Configuration & Initialization

Typically, the `NetworkApplicationBuilder` handles registry initialization. If you are using the Codec standalone:

```csharp
// Optional: Configure a pool manager for zero-allocation packet recycling
PacketRegistry.Configure(myPoolManager);

// Freeze the registry (finalizes the catalog)
PacketRegistry.Build();
```

### 2. Manual Deserialization

The registry provides a high-performance `TryDeserialize` method that handles header extraction and object rehydration (or pooling).

```csharp
// Read a packet from a raw buffer span
if (PacketRegistry.TryDeserialize(bufferLease.Span, out IPacket? packet))
{
    using (packet) // Packets are usually IPoolable
    {
        Console.WriteLine($"Received packet: {packet.GetType().Name}");
    }
}
```

## Practical notes

- **Magic Numbers**: Magic numbers are 4-byte hashes derived from the packet's full type name. They provide collision-resistant identification without the overhead of strings.
- **Built-in Packets**: Built-in frames like `Handshake`, `Control`, and `SessionResume` are automatically registered by the framework.
- **Threading**: `PacketRegistry` is thread-safe after `Build()` is called.

## Related APIs

- [Frame Model](./frame-model.md)
- [Built-in Frames](./built-in-frames.md)
- [Packet Contracts](../../abstractions/packet-contracts.md)
- [SDK Overview](../../sdk/index.md)
- [Packet Dispatch](../../runtime/routing/packet-dispatch.md)
- [Network Application (Hosting)](../../hosting/network-application.md)
