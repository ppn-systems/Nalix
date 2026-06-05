# Unified Packet System

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Architecture](architecture.md)

The Packet System is the foundation of Nalix's networking model. It provides a declarative way to define network messages that are compatible with the packet registry and serializer used by the runtime and SDK.

## 1. Defining Packets

### The Common Attribute Pair

For a standard user packet, the common pattern is to apply:

1. **`[Packet]`** when you want attribute-based discovery.
2. **`[SerializePackable]`** to opt the type into Nalix serialization.

```csharp
using Nalix.Codec.DataFrames;
using Nalix.Abstractions.Serialization;

[Packet] // Discovery & Registration
[SerializePackable] // Defaults to SerializeLayout.Auto
public sealed class TradePacket : PacketBase<TradePacket>
{
    public const ushort OpCodeValue = 0x5001;

    public long TradeId { get; set; }
    public double Price { get; set; }

    public TradePacket() => OpCode = OpCodeValue;
}
```

### Serialization Attributes

| Attribute | Purpose |
| :--- | :-- |
| `[Packet]` | Marks a class for attribute-based discovery. |
| `[SerializePackable]` | Marks a class for serialization. Required on all packet types. |
| `[SerializeOrder(int)]` | Sets the explicit position of a field in the byte stream (Explicit layout only). |
| `[SerializeDynamicSize(int)]` | Defines the maximum byte limit for variable-length strings or arrays. |
| `[SerializeIgnore]` | Excludes a property from network serialization. |
| `[SerializeHeader]` | Maps a property to a specific header region (advanced use). |

---

## 2. Serialization Layouts

The `[SerializePackable]` attribute requires a `SerializeLayout` value that controls how fields are ordered in the byte stream.

| Layout | Behavior | Member Discovery | Recommended for |
| --- | --- | --- | --- |
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

For a detailed technical breakdown of how each type is encoded and sorted on the wire, see the [Binary Specification](../serialization/binary-spec.md).

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

`[SerializeOrder]` values are relative to the start of the payload. The system header (opcode, flags, priority, sequence id) is managed internally by `PacketBase<T>`. Always start your payload order from 0.

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
    `[SerializeOrder]` values do not overlap with the system header. The header fields (opcode, flags, priority, sequence id) are managed by the framework. Always start payload fields from order `0`.

---

## 4. Packet Versioning

For long-lived public wire contracts, treat versioning as an application-level compatibility discipline and prefer additive evolution:

1. **Use Explicit layout.** Never change the order of existing fields.
2. **Append new fields.** Add new fields with a higher `[SerializeOrder]` value.
3. **Compatibility discipline.** Avoid reordering or retyping existing fields if older and newer builds must interoperate.

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
[SerializePackable]
public sealed class PingRequest : PacketBase<PingRequest>
{
    public const ushort OpCodeValue = 0x1001;

    public string Message { get; set; } = string.Empty;

    public PingRequest() => OpCode = OpCodeValue;
}
```

---

## 6. Edge Cases & Wire Integrity

### OpCode Validation: Ensuring Type Safety

Every packet in Nalix defines a static **OpCode** via the `IPacketStaticOpcode` interface. During deserialization, `PacketBase<T>.Deserialize` validates the OpCode in the wire header against `TSelf.StaticOpCode` before attempting to read the payload.

If you attempt to deserialize a `PingRequest` buffer into a `TradePacket` object, the system will throw a `SerializationFailureException` due to an OpCode mismatch. This prevents silent data corruption and type-conversion bugs.

### Handling Serialization Failures

Network data can be malformed, truncated, or malicious. Nalix protects the hot path by throwing a `SerializationFailureException` in the following cases:

- **Buffer Too Small**: The incoming data is shorter than the fixed-size header or the expected static payload.
- **Dynamic Size Limit Exceeded**: A string or array exceeds the limit set by `[SerializeDynamicSize]`.
- **OpCode Mismatch**: The incoming packet's OpCode does not match the target deserialization class.

#### Best Practice: Defensive Dispatch

In a high-performance scenario, let the dispatcher handle these exceptions. It will log the failure, increment the connection's error count via `IConnection.IncrementErrorCount()`, and safely return the buffer to the pool.

```csharp
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

try 
{
    var packet = MyPacket.Deserialize(buffer);
}
catch (SerializationFailureException ex)
{
    Log.Warning($"Discarding malformed packet: {ex.Message}");
    // Passive health tracking
    connection.IncrementErrorCount();
}
```

---

## 7. Packet Registration

Packets must be registered with the `PacketRegistry` before they can be deserialized at runtime. Nalix uses a **Source Generator** to automatically handle most of this work.

### Automatic registration (hosted server)

When using `Nalix.Hosting`, the framework automatically discovers and registers all packet types in your project during the `Build()` phase.

```csharp
using Nalix.Hosting;

var app = NetworkApplication.CreateBuilder()
    .AddHandler<PingHandler>()
    .Build(); // PacketRegistry.Build() is called internally
```

### Manual registration (Standalone Codec)

If you are using `Nalix.Codec` without the hosting layer, you must manually initialize the registry. The Source Generator still generates the registration calls, but you must "freeze" the catalog before use.

```csharp
using Nalix.Codec.DataFrames;

// 1. (Optional) Configure pooling
PacketRegistry.Configure(poolManager);

// 2. Build the registry (freezes the catalog)
PacketRegistry.Build();
```

Built-in signal packets (`Control`, `SessionInit`, `SessionChallenge`, `SessionProof`, `SessionEstablished`, `SessionTofu`, `SessionResume`, `Directive`, `TimeSync`) are registered automatically by the framework.

---

## 6. Custom Formatters

If your data type is not supported by the built-in serializer (e.g., a third-party struct), you can implement a custom formatter.

### Example: Complete Custom Formatter

In this scenario, we have a `UserProfile` class that we want to shared between server and client, but it requires a specialized serialization format (e.g., to handle legacy bit-flags or custom string encoding).

```csharp
using System;
using Nalix.Codec.Serialization;
using Nalix.Abstractions.Serialization;

// 1. Define your data contract (shared)
public sealed class UserProfile
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
}

// 2. Implement the specialized formatter
public sealed class UserProfileFormatter : IFormatter<UserProfile>
{
    public void Serialize(ref DataWriter writer, in UserProfile value)
    {
        writer.WriteInt32(value.UserId);
        writer.WriteString(value.DisplayName);
        writer.WriteInt64(value.LastSeen.ToBinary());
    }

    public UserProfile Deserialize(ref DataReader reader)
    {
        return new UserProfile
        {
            UserId = reader.ReadInt32(),
            DisplayName = reader.ReadString(),
            LastSeen = DateTime.FromBinary(reader.ReadInt64())
        };
    }
}

// 3. Register the formatter during startup
LiteSerializer.Register<UserProfile>(new UserProfileFormatter());
```

1. Implement `IFormatter<T>`.
2. Register it using `LiteSerializer.Register<T>(formatter)`.

```csharp
using Nalix.Codec.Serialization;
using Nalix.Abstractions.Serialization;

public class GeoLocationFormatter : IFormatter<GeoLocation>
{
    public void Serialize(ref DataWriter writer, in GeoLocation value)
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

---

## 7. Ultra Hot Path: Raw Memory Packets

For scenarios where even deserialization overhead is unacceptable (e.g., relaying data or custom binary formats), Nalix supports the **MemoryPacket** bypass.

### Using `MemoryPacket`

A `MemoryPacket` is a high-performance struct that holds a `ReadOnlyMemory<byte>` payload and the standard `PacketHeader`.

```csharp
using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;

// Receiving raw memory directly in a handler
[PacketOpcode(0x9000)]
public ValueTask HandleRaw(ReadOnlyMemory<byte> rawData, IConnection connection)
{
    // rawData points directly into the transport buffer lease segment
    return ValueTask.CompletedTask;
}
```

When a handler signature accepts `ReadOnlyMemory<byte>`, the Nalix dispatcher **skips the packet registry lookup** and provides the raw buffer segment directly.

---

## 8. See it in action

- [Quickstart](../../quickstart.md) — Define and use your first packets.
- [TCP Request/Response](../../guides/networking/tcp-patterns.md) — See how packet contracts are shared between projects.
- [UDP Auth Flow](../../guides/networking/udp-security.md) — Observe packets used for authenticated session resumption.
