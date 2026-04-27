# Nalix.Codec

`Nalix.Codec` handles the transformation of data between objects and wire formats. It includes serialization, compression, and security transforms.

## Key Responsibilities

- **Serialization**: Fast, low-allocation binary serialization for packets.
- **Compression**: Integrated LZ4 compression for reducing network bandwidth.
- **Security**: Framed packet encryption and hashing.
- **Memory**: Efficient buffer leasing and IO primitives (`DataReader`, `DataWriter`).

## Where it fits

```mermaid
flowchart LR
    A["Nalix.Codec"] --> B["Serialization"]
    A --> C["Transforms"]
    A --> D["Memory"]
    B --> E["Nalix.Network"]
    B --> F["Nalix.SDK"]
```

## Core Components

### `LiteSerializer`

A high-performance binary serializer that uses attributes to define layout.

### `BufferLease`

A lightweight wrapper around pooled memory that ensures safe disposal and reuse.

### `FrameCipher` and `FrameCompression`

Helpers for applying encryption and compression to framed packets.


### `LZ4Codec`

A pooled implementation of the LZ4 compression algorithm.

## Registry flow

```mermaid
flowchart LR
    A["PacketRegistryFactory"] --> B["Include assemblies / namespaces"]
    B --> C["CreateCatalog()"]
    C --> D["PacketRegistry"]
    D --> E["Nalix.Network"]
    D --> F["Nalix.SDK"]
```

### Purpose

- Define built-in frames.
- Build an immutable packet registry.
- Provide shared serialization helpers.
- Provide pooled LZ4 compression primitives.
- Provide shared framed packet transform helpers (`FrameCipher` and `FrameCompression`).

### Key components

- `FrameBase` / `PacketBase<TSelf>` Ã¢â‚¬â€ base abstractions for headers, auto-magic, serialization, and pooling.
- `SerializePackableAttribute` / `SerializeOrderAttribute` / `SerializeIgnoreAttribute` / `SerializeHeaderAttribute` / `SerializeDynamicSizeAttribute` Ã¢â‚¬â€ low-level serialization layout controls.
- `LiteSerializer` / `FormatterProvider` / `IFormatter<T>` Ã¢â‚¬â€ serializer entry points and formatter resolution.
- `DataReader` / `DataWriter` / `HeaderExtensions` Ã¢â‚¬â€ low-level read/write and header inspection helpers.
- `PacketRegistryFactory` Ã¢â‚¬â€ scans packet types and binds deserialize function pointers.
- `PacketRegistry` Ã¢â‚¬â€ frozen catalog of deserializers/transformers.
- `Handshake` Ã¢â‚¬â€ default handshake frame used to exchange ephemeral keys, nonces, proofs, and transcript hash.
- `SessionResume` Ã¢â‚¬â€ unified session signal packet for resume request/response flows (uses `SessionResumeStage` for stage disambiguation).
- `Control` Ã¢â‚¬â€ built-in frame type.
- `PacketProvider<TPacket>` Ã¢â‚¬â€ packet initialization and pooling helpers.
- `FragmentHeader` / `FragmentAssembler` / `FragmentOptions` Ã¢â‚¬â€ chunk large payloads and reassemble them safely.
- `FrameCipher` / `FrameCompression` Ã¢â‚¬â€ framed packet encrypt/decrypt and compress/decompress helpers.
- `LZ4Codec` Ã¢â‚¬â€ pooled block compression and decompression.

### Quick example

```csharp
using Nalix.Codec.DataFrames;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Codec.Memory;

// Build and register the shared catalog
PacketRegistryFactory factory = new();
IPacketRegistry registry = factory.CreateCatalog();
InstanceManager.Instance.Register<IPacketRegistry>(registry);

// Handshake frame
Handshake hs = new(
    HandshakeStage.CLIENT_HELLO,
    Csprng.GetBytes(32),
    Csprng.GetBytes(32),
    flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);
hs.UpdateTranscriptHash("nalix-default-handshake"u8);
byte[] bytes = hs.Serialize();
```

### Registry build flow

- Add assemblies or namespaces if you have custom packets.
- Call `CreateCatalog()` once and reuse the result in listeners and clients.

### Quick example

```csharp
PacketRegistryFactory factory = new();
factory.IncludeNamespaceRecursive("MyApp.Packets");
IPacketRegistry catalog = factory.CreateCatalog();
```

## Key API pages

- [Serialization](../api/codec/serialization/serialization-basics.md)
- [Buffer Management](../api/framework/memory/buffer-management.md)
- [LZ4](../api/codec/lz4.md)
- [Frame Model](../api/codec/packets/frame-model.md)
- [Packet Registry](../api/codec/packets/packet-registry.md)
- [Built-in Frames](../api/codec/packets/built-in-frames.md)
- [Fragmentation](../api/codec/packets/fragmentation.md)
- [Cryptography](../api/security/cryptography.md)

