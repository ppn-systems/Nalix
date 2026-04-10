# Packet System

The Packet System is the core of Nalix. It provides a declarative way to define network messages that are high-performance, version-safe, and zero-allocation.

## 1. Defining Packets

To define a packet, create a `sealed class` that inherits from `PacketBase<T>` and annotate it with `[SerializePackable]`.

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class TradePacket : PacketBase<TradePacket>
{
    public const ushort OpCodeValue = 0x5001;

    [SerializeOrder(0)] // Starts immediately after system header (offset 13)
    public long TradeId { get; set; }

    [SerializeOrder(1)]
    public double Price { get; set; }

    public TradePacket() => OpCode = OpCodeValue;
}
```

### Serialization Attributes

| Attribute | Purpose |
|---|---|
| `[SerializePackable]` | Marks a class for source-generated serialization. |
| `[SerializeOrder(int)]` | Sets the explicit position of a field in the byte stream. |
| `[SerializeDynamicSize(int)]` | Defines the maximum size (bytes) for variable-length strings or arrays. |
| `[SerializeIgnore]` | Excludes a property from network serialization. |
| `[SerializeHeader]` | Maps a property to a specific header region (Advanced). |

---

## 2. Common Use Cases

### Case A: Strings and Arrays
Variable-length data requires `[SerializeDynamicSize]` to protect against buffer overflow and large allocation attacks.

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class ChatMessage : PacketBase<ChatMessage>
{
    [SerializeOrder(0)]
    [SerializeDynamicSize(32)] // Max 32 bytes for nickname
    public string From { get; set; } = string.Empty;

    [SerializeOrder(1)]
    [SerializeDynamicSize(1024)] // Max 1KB for message
    public string Text { get; set; } = string.Empty;

    [SerializeOrder(2)]
    [SerializeDynamicSize(5)] // Max 5 items in array
    public int[] RecipientIds { get; set; } = [];
}
```

### Case B: Enums
Enums are serialized as their underlying numeric type (usually `int32` or `uint8`).

```csharp
public enum OrderStatus : byte { PENDING = 1, FILLED = 2, CANCELED = 3 }

[SerializePackable(SerializeLayout.Explicit)]
public sealed class OrderUpdate : PacketBase<OrderUpdate>
{
    [SerializeOrder(0)]
    public OrderStatus Status { get; set; }
}
```

### Case C: Explicit Layout & Relative Positioning
In Nalix, `[SerializeOrder]` is relative to the start of the payload. The system automatically handles the preceding header.

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class RegionalPacket : PacketBase<RegionalPacket>
{
    // Order 0 starts at offset 13 (immediately after header)
    [SerializeOrder(0)]
    public ushort RegionId { get; set; } 

    [SerializeOrder(1)]
    public string Data { get; set; } = string.Empty;
}
```

> [!IMPORTANT]
> `[SerializeOrder]` values do not overlap with the system header. The header fields (Magic, OpCode, etc.) are managed internally. Always start your payload `Order` from 0.

---

## 3. Advanced Packet Metadata

Metadata attributes allow you to define runtime behavior directly on your packet contracts or handlers.

| Attribute | Behavior |
|---|---|
| `[PacketOpcode(ushort)]` | Maps a method to handle a specific OpCode. |
| `[PacketPermission(level)]` | Enforces authorization rules via middleware. |
| `[PacketTimeout(ms)]` | Sets a per-packet execution timeout. |
| `[PacketRateLimit(burst, rate)]` | Protects the server from spamming specific packets. |
| `[PacketConcurrencyLimit(count)]` | Limits how many instances of this packet process simultaneously. |

---

## 4. Packet Versioning

Nalix supports versioning through **Additive Evolution**:

1. **Explicit Order**: Always use `[SerializeOrder]`. Never change the order of existing fields.
2. **Backward Compatibility**: To add a new field, simply use a higher `SerializeOrder`. Older clients reading newer packets will simply ignore the trailing bytes. Newer clients reading older packets will receive default values for the missing fields.

**Example: Adding a field**
```csharp
// Version 1
[SerializeOrder(0)] public int Id { get; set; }

// Version 2 (SAFE)
[SerializeOrder(0)] public int Id { get; set; }
[SerializeOrder(1)] public string? Tags { get; set; } // Added at the end
```

---

## 5. Custom Formatters

If your data type is not supported out-of-the-box (e.g., a third-party GeoLocation struct), you can implement a custom formatter.

### Steps:
1. Implement `IFormatter<T>`.
2. Register it using `LiteSerializer.Register<T>(formatter)`.

```csharp
public class MyCustomFormatter : IFormatter<MyCustomData>
{
    public void Serialize(ref DataWriter writer, MyCustomData value)
    {
        writer.WriteInt32(value.X);
        writer.WriteInt32(value.Y);
    }

    public MyCustomData Deserialize(ref DataReader reader)
    {
        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        return new MyCustomData(x, y);
    }
}
```

## Recommended Next Steps

- [Performance Optimizations](./performance-optimizations.md)
- [Middleware Implementation](./middleware.md)
- [Production End-to-End Guide](../guides/production-end-to-end.md)
