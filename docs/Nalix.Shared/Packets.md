# Packet & Frame API — Base Classes for Serialization/Transport Protocols

This library provides a flexible, high-performance foundation for defining, serializing, transforming, and efficiently transmitting custom network packets ("frames") using explicit headers, object pooling, and generic infrastructure.

- **Namespaces:**  
  `Nalix.Shared.Frames`, `Nalix.Shared.Frames.Controls`, `Nalix.Common.Networking.Packets.Abstractions`
- **Base classes:** `FrameBase`, `PacketBase<TSelf>`
- **Key interfaces:** `IPacket`, `IPacketDeserializer<T>`, `IPacketTransformer<T>`, ...  
- **Supported features:** header fields, frame type registration, static serialization, object pooling, automatic magic number, protocol/priority/flags.

---

## Architecture

- All packets **must** inherit from `FrameBase` (abstract) or the generic `PacketBase<TSelf>` (recommended).
- `PacketBase<TSelf>` implements base serialization, deserialization, pooling, length calculation, magic number, and core protocol fields.
- Specialized packets (such as `Control`, `Directive`, `Handshake`) extend `PacketBase<T>`, define wire fields via attributes, and may implement sequencing, timestamp, or reason interfaces for protocol work.

---

## Core API

### FrameBase

The base abstract class for all serialized packets:

| Member                  | Type             | Description                                |
|-------------------------|------------------|--------------------------------------------|
| `Length`                | `ushort`         | Total serialized length (header + content) |
| `MagicNumber`           | `uint`           | Unique FNV-1a hash-based id for type       |
| `OpCode`                | `ushort`         | Operation or command code                  |
| `Flags`                 | `PacketFlags`    | Bitwise property flags (see below)         |
| `Priority`              | `PacketPriority` | Processing/delivery priority               |
| `Protocol`              | `ProtocolType`   | Target protocol (TCP/UDP/...)              |
| `ResetForPool()`        | `void`           | Reset fields before/after pooling          |
| `Serialize()`           | `byte[]`         | Serialize to new byte[]                    |
| `Serialize(Span<byte>)` | `int`            | Serialize into given buffer                |

---

### PacketBase<`TSelf`>

Recommended base class for custom derived packets.  
Adds:

- **Automatic static serialization** (via `LiteSerializer`)
- **Pooling** for allocation-free reuse
- **Default magic number** unique per type (no attribute required)
- **Reflection & wire layout cache** — fast, attribute-driven, minimal per-packet overhead

Sample interface:

```csharp
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IPacketDeserializer<TSelf>
    where TSelf : PacketBase<TSelf>, new()
{
    // Static: Auto-magic, property/field wire layout
    static uint AutoMagic = ...;
    static PropertyMetadata[] Metadata = ...; // Fast serialization layout cache

    public override ushort Length { get; }
    public override byte[] Serialize() { ... }
    public override int Serialize(Span<byte> buffer) { ... }

    public static TSelf Deserialize(ReadOnlySpan<byte> buffer); // Pooling-aware
    public static TSelf Encrypt(TSelf packet, byte[] key, CipherSuiteType algo);
    public static TSelf Decrypt(TSelf packet, byte[] key);

    public override void ResetForPool() { ... }
}
```

Features:

- Handles all `Length`, `Serialize`, `ResetForPool`, and registration
- Wire order/layout set via `[SerializeOrder]`, `[SerializeDynamicSize]` attributes
- Supports fixed and dynamic-sized field types
- Pooling enabled for all packets (avoid allocating every deserialize!)

---

## Packet Header Layout

Fields and wire offsets (see `PacketHeaderOffset`):

| Field         | Offset | Type   | Purpose                                   |
|---------------|--------|--------|-------------------------------------------|
| MagicNumber   | 0      | uint32 | Unique type signature (FNV-1a)            |
| OpCode        | 4      | uint16 | Operation command                         |
| Flags         | 6      | byte   | See `PacketFlags`: COMPRESSED, ENCRYPTED… |
| Priority      | 7      | byte   | See `PacketPriority`: NONE, HIGH, URGENT… |
| Protocol      | 8      | byte   | See `ProtocolType`: TCP, UDP, etc.        |
| Data fields   | 9+     | ...    | Custom fields for actual payload          |

> Use `[SerializeOrder(PacketHeaderOffset.FIELD)]` on properties to control serialization order.

---

## Built-in Extension Interfaces

- `IPacketDeserializer<T>` — Static `Deserialize(ReadOnlySpan<byte>)`
- `IPacketTransformer<T>` — Static compression, encryption, transformation for your type
- `IPoolable` — Reusable by object pool; always call `ResetForPool()`
- `IPacketSequenced` — `uint SequenceId` (for correlation)
- `IPacketTimestamped` — `long Timestamp` + monotonic measurement
- `IPacketReasoned` — A reason code for errors/nacks/system causes

---

## Custom Packet Example

```csharp
public sealed class Ping : PacketBase<Ping>, IPacketSequenced
{
    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 0)]
    public uint SequenceId { get; set; }

    [SerializeOrder(PacketHeaderOffset.DATA_REGION + 1)]
    public string Message { get; set; }

    public override void ResetForPool()
    {
        base.ResetForPool();
        SequenceId = 0;
        Message = string.Empty;
    }
}
```

---

### Example: Serialize / Deserialize

```csharp
var packet = new Control();
packet.Initialize(ControlType.PING, 123u);
byte[] buf = packet.Serialize();

Control restored = Control.Deserialize(buf);
// Use as needed
```

---

## Built-in Packet Field Types

- Byte arrays, strings, enums, integers (all fixed/dynamic)
- Dynamic size: `[SerializeDynamicSize(MIN)]` supports strings/byte arrays
- All fields with `[SerializeOrder(X)]` are serialized in order

---

## PacketFlags Cheat Sheet

| Flag         | Meaning/Use                                               |
|--------------|-----------------------------------------------------------|
| COMPRESSED   | Payload is compressed; decompress before handling         |
| ENCRYPTED    | Payload is encrypted; decrypt before use                  |
| FRAGMENTED   | Split/fragmented packet; reassemble if needed             |
| RELIABLE     | Sent via reliable protocol (TCP)                          |
| UNRELIABLE   | Sent via best-effort protocol (UDP)                       |
| ACKNOWLEDGED | Packet has been acknowledged                              |
| SYSTEM       | System/internal control packet (not user data)            |

> Combine with bitwise OR: `Flags = PacketFlags.COMPRESSED | PacketFlags.ENCRYPTED;`

---

## Advanced: Pooling and Object Lifecycle

- All pooled packets **must** call `ResetForPool()` on reuse for safety
- Use your pool manager to `Return()` or `Get<T>()` as needed
- `Length` property is always up-to-date

---

## Best Practices

- Always assign `[SerializeOrder]` with the right `PacketHeaderOffset` to all protocol fields
- Use `PacketBase<TSelf>` for all custom implementations unless you need manual serialization
- Use static `Deserialize(buf)`, `Encrypt`, and `Decrypt` for efficient transforms
- Reset objects before reuse (especially when using object pooling)
- Prefer immutable property layout for critical protocol types

---

## License

Licensed under the Apache License, Version 2.0.
