# 🌀 Nalix AI Skill — Deep Serialization & Data Layout

This skill provides expertise in the Nalix serialization system (`LiteSerializer`), focusing on high-performance binary protocols and zero-allocation data manipulation.

---

## 🛠️ Core Components

- **`LiteSerializer`**: The primary entry point for serializing and deserializing POCOs.
- **`DataReader` / `DataWriter`**: High-level abstractions over `ReadOnlySpan<byte>` and `Span<byte>`.
- **`IFormatter<T>`**: Contract for manual serialization logic.
- **`SerializePackableAttribute`**: Applied to classes/structs to enable source-generated or reflection-based serialization.

---

## 📜 Layout Rules

### Explicit Layout
When using `[SerializePackable(SerializeLayout.Explicit)]`, every member must have a `[SerializeOrder(N)]`.

- **Reserved Header:** In `PacketBase` types, the first few bytes are reserved for the header. Payload data should typically start at `PacketHeaderOffset.Region` (usually order 0 in payload-only logic, but beware of overlap).
- **Uniqueness:** `SerializeOrder` must be unique per type (NALIX014).
- **Gaps:** Large gaps in `SerializeOrder` are allowed but flagged (NALIX046).

### Member Types
- **Primitives:** `int`, `long`, `float`, etc., are serialized in Little-Endian.
- **Strings:** Serialized as UTF-8 with a length prefix.
- **Arrays/Collections:** Serialized with a length prefix. Use `[SerializeDynamicSize]` for clarity (NALIX016).
- **Nested Packables:** Handled recursively.

---

## ⚡ Performance Optimization

### Zero-Allocation Rehydration
Nalix supports "Filling" an existing instance instead of creating a new one.

- **`IFillableFormatter<T>`**: Implement this to allow rehydrating pooled objects.
- **Pattern:** `fillable.Fill(ref reader, existingInstance)`.

### Segmented Memory (LOH Avoidance)
- **`IBufferWriter<byte>`**: Always prefer writing to an `IBufferWriter` (like `PipeWriter`) to avoid large contiguous byte array allocations.
- **`ReadOnlySequence<byte>`**: Support reading from non-contiguous segments using `SequenceReader<byte>`.

---

## 🛡️ Common Pitfalls

- **`SerializeIgnore` vs `SerializeOrder`**: You cannot have both on the same member (NALIX015).
- **Negative Orders**: Do not use negative values for `SerializeOrder` (NALIX021).
- **Large Objects**: Objects > 85KB should be serialized using segmented writing to avoid the Large Object Heap (LOH).

---

## 🧪 Implementation Pattern

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class TradePacket : PacketBase<TradePacket>
{
    [SerializeOrder(0)]
    public long ItemId { get; set; }

    [SerializeOrder(1)]
    public int Quantity { get; set; }

    [SerializeOrder(2)]
    [SerializeDynamicSize]
    public string Note { get; set; } = string.Empty;

    public static TradePacket Deserialize(ReadOnlySpan<byte> data)
    {
        var reader = new DataReader(data);
        return LiteSerializer.Deserialize<TradePacket>(ref reader);
    }
}
```
