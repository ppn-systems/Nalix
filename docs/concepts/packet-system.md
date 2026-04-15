# Packet System

The Packet System is the foundation of Nalix's networking model. It provides a declarative way to define network messages that are high-performance, version-safe, and zero-allocation.

## 1. Defining Packets

To define a packet, create a `sealed class` that inherits from `PacketBase<T>` and annotate it with both `[Packet]` (for discovery) and `[SerializePackable]` (for wire layout).

```csharp
[Packet]
[SerializePackable(SerializeLayout.Explicit)]
public sealed class TradePacket : PacketBase<TradePacket>
{
    public const ushort OpCodeValue = 0x5001;

    [SerializeOrder(0)]
    public long TradeId { get; set; }

    [SerializeOrder(1)]
    public double Price { get; set; }

    public TradePacket() => OpCode = OpCodeValue;
}
```

### Serialization Attributes

| Attribute | Purpose |
| :--- | :-- |
| `[Packet]` | Marks a class for automatic discovery and registration. Recommended for all user packets. |
| `[SerializePackable]` | Marks a class for serialization. Required on all packet types. |
| `[SerializeOrder(int)]` | Sets the explicit position of a field in the byte stream (Explicit layout only). |
| `[SerializeDynamicSize(int)]` | Defines the maximum byte limit for variable-length strings or arrays. |
| `[SerializeIgnore]` | Excludes a property from network serialization. |
| `[SerializeHeader]` | Maps a property to a specific header region (advanced use). |

---

## 2. Serialization Layouts

The `[SerializePackable]` attribute requires a `SerializeLayout` value that controls how fields are ordered in the byte stream.

| Layout | Behavior | Member Discovery | Recommended for |
|---|---|---|---|
| `SerializeLayout.Auto` | Reorders fields to minimize padding (typically by size descending). | All public properties/fields except those marked with `[SerializeIgnore]`. | Internal DTOs where compact size matters and version stability is not critical. |
| `SerializeLayout.Sequential` | Preserves source code order. | All public properties/fields except those marked with `[SerializeIgnore]`. | Simple packets where source order is intuitive. |
| `SerializeLayout.Explicit` | Orders fields by `[SerializeOrder]` values. Only includes annotated members. | Only members decorated with `[SerializeOrder]`. | **Production packets.** Recommended for all public-facing network definitions. |

```csharp
[SerializePackable(SerializeLayout.Sequential)]
public sealed class SimplePacket : PacketBase<SimplePacket>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

!!! warning "Auto layout and version stability"
    In `SerializeLayout.Auto`, adding a new field may change the byte offsets of all existing fields because the serializer re-sorts them. **Always use `Explicit` for public-facing network packets.**

---

## 3. Common Use Cases

### Case A: Strings and Arrays

Variable-length data requires `[SerializeDynamicSize]` to define a maximum byte size. This protects against buffer overflow and large-allocation attacks.

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class ChatMessage : PacketBase<ChatMessage>
{
    [SerializeOrder(0)]
    [SerializeDynamicSize(32)]
    public string From { get; set; } = string.Empty;

    [SerializeOrder(1)]
    [SerializeDynamicSize(1024)]
    public string Text { get; set; } = string.Empty;

    [SerializeOrder(2)]
    [SerializeDynamicSize(5)]
    public int[] RecipientIds { get; set; } = [];
}
```

### Case B: Enums

Enums are serialized as their underlying numeric type.

```csharp
public enum OrderStatus : byte { PENDING = 1, FILLED = 2, CANCELED = 3 }

[SerializePackable(SerializeLayout.Explicit)]
public sealed class OrderUpdate : PacketBase<OrderUpdate>
{
    [SerializeOrder(0)]
    public OrderStatus Status { get; set; }
}
```

### Case C: SerializeOrder and the Header

`[SerializeOrder]` values are relative to the start of the payload. The system header (magic number, opcode, protocol metadata) is managed internally by `PacketBase<T>`. Always start your payload order from 0.

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class RegionalPacket : PacketBase<RegionalPacket>
{
    [SerializeOrder(0)]
    public ushort RegionId { get; set; }

    [SerializeOrder(1)]
    [SerializeDynamicSize(256)]
    public string Data { get; set; } = string.Empty;
}
```

!!! note "Header layout"
    `[SerializeOrder]` values do not overlap with the system header. The header fields (magic, opcode, protocol, priority) are managed by the framework. Always start payload fields from order `0`.

---

## 4. Packet Versioning

Nalix supports versioning through **additive evolution**:

1. **Use Explicit layout.** Never change the order of existing fields.
2. **Append new fields.** Add new fields with a higher `[SerializeOrder]` value.
3. **Backward compatibility.** Older clients reading newer packets will ignore trailing bytes. Newer clients reading older packets will receive default values for missing fields.

```csharp
// Version 1
[SerializeOrder(0)] public int Id { get; set; }

// Version 2 — adding a field at the end is safe
[SerializeOrder(0)] public int Id { get; set; }
[SerializeOrder(1)] public string? Tags { get; set; }
```

!!! warning "Breaking changes"
    The following changes break wire compatibility:
    
    - Changing the `[SerializeOrder]` of an existing field
    - Removing a field without renumbering successors
    - Changing a field's type
    - Switching from `Explicit` to `Auto` layout

---

## 5. Sharing Packets (Server & Client)

Packets are usually defined in a shared **Contracts** project referenced by both the Server and Client. This ensures both sides use the exact same wire layout and attributes.

```csharp
// Example: Defined in a shared 'Contracts' project
[Packet]
[SerializePackable(SerializeLayout.Explicit)]
public sealed class PingRequest : PacketBase<PingRequest>
{
    public const ushort OpCodeValue = 0x1001;

    [SerializeOrder(0)]
    [SerializeDynamicSize(64)]
    public string Message { get; set; } = string.Empty;

    public PingRequest() => OpCode = OpCodeValue;
}
```

---

## 6. Packet Registration

Packets must be registered with the `PacketRegistry` before they can be deserialized at runtime. The `PacketRegistryFactory` discovers packet types, binds their deserializers, and builds an immutable `FrozenDictionary`-backed catalog.

### Automatic registration (hosted server)

When using `Nalix.Network.Hosting`, use the builder to scan assemblies:

```csharp
var app = NetworkApplication.CreateBuilder()
    .AddPacket<PingRequest>()        // Scans the assembly containing PingRequest
    .AddHandlers<PingHandler>()
    .Build();
```

### Manual registration

When building the dispatch manually, create the registry explicitly:

```csharp
PacketRegistryFactory factory = new();
factory.RegisterPacket<PingRequest>()
       .RegisterPacket<PingResponse>();

// Or scan by namespace
factory.IncludeAssembly(typeof(PingRequest).Assembly);
factory.IncludeNamespaceRecursive("MyApp.Packets");

IPacketRegistry catalog = factory.CreateCatalog();
```

Built-in signal packets (`Control`, `Handshake`, `SessionResume`, `Directive`) are registered automatically by the `PacketRegistryFactory` constructor.

---

## 6. Custom Formatters

If your data type is not supported by the built-in serializer (e.g., a third-party struct), you can implement a custom formatter.

1. Implement `IFormatter<T>`.
2. Register it using `LiteSerializer.Register<T>(formatter)`.

```csharp
public class GeoLocationFormatter : IFormatter<GeoLocation>
{
    public void Serialize(ref DataWriter writer, GeoLocation value)
    {
        writer.WriteInt32(value.X);
        writer.WriteInt32(value.Y);
    }

    public GeoLocation Deserialize(ref DataReader reader)
    {
        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        return new GeoLocation(x, y);
    }
}
```

## See it in action

- [Quickstart](../quickstart.md) — Define and use your first packets.
- [TCP Request/Response](../guides/tcp-request-response.md) — See how packet contracts are shared between projects.
- [UDP Auth Flow](../guides/udp-auth-flow.md) — Observe packets used for authenticated session resumption.
