# LiteSerializer — Fast, Compact, Customizable Serialization

**LiteSerializer** is a high-performance, allocation-optimized binary serializer for .NET, designed for efficient object/struct serialization, high-throughput, and explicit memory control.  
Suited for networking, logging, interprocess communication, distributed protocols, and low-latency storage scenarios.

- **Namespace:** `Nalix.Shared.Serialization`
- **Class:** `LiteSerializer` (static)
- **Key Features:**
  - Fast serialization of unmanaged types, structs, arrays, and common .NET types
  - Allocation-free APIs (`Span<byte>`, buffer reuse, BufferLease support)
  - Custom serializer registration and full control with wire layout attributes
  - Minimal overhead, zero boxing/copy for value types

---

## Typical Usage

### Serialize / Deserialize (Basic Usage)

```csharp
using Nalix.Shared.Serialization;

// Serialize value to byte[]
byte[] payload = LiteSerializer.Serialize(myData);

// Deserialize from byte[]
MyType restored = default!;
LiteSerializer.Deserialize(payload, ref restored);
```

### In-place Span/Buffer Serialization

```csharp
Span<byte> buffer = stackalloc byte[128];
int bytesWritten = LiteSerializer.Serialize(myData, buffer);
// ... Send or save buffer[..bytesWritten]
```

### Allocation-free — BufferLease

```csharp
using Nalix.Shared.Memory.Buffers;

// Allocate an output buffer lease (pooled)
using var lease = LiteSerializer.Serialize(myData, zeroOnDispose: true);
// lease.Memory/lease.Span contains payload, lease.Length gives size

// Deserialize directly from BufferLease
MyType result = default!;
LiteSerializer.Deserialize(lease, ref result);
```

---

## API Overview

| Method                                                      | Returns            | Purpose                                                           |
|-------------------------------------------------------------|--------------------|-------------------------------------------------------------------|
| `Serialize<T>(in T value)`                                  | `byte[]`           | Serialize value to a new byte array                               |
| `Serialize<T>(in T value, Span<byte> buffer)`               | `int`              | Serialize to existing buffer (returns length written)             |
| `Serialize<T>(in T value, BufferLease lease)`               | `int`              | Serialize to BufferLease                                          |
| `Serialize<T>(in T value, bool zeroOnDispose)`              | `BufferLease`      | Allocate a buffer lease and serialize (optionally zero on dispose)|
| `Deserialize<T>(ReadOnlySpan<byte> buffer, ref T outValue)` | `int`              | Deserialize from bytes into an existing object                    |
| `Deserialize<T>(BufferLease lease, ref T outValue)`         | `int`              | Deserialize from BufferLease                                      |
| `Deserialize<T>(ReadOnlySpan<byte> buffer, out int len)`    | `T`                | Deserialize, returns value and bytes-read                         |
| `Deserialize<T>(BufferLease lease, out int len)`            | `T`                | Deserialize from lease (returns value + bytes-read)               |
| `Register<T>(IFormatter<T> formatter)`                      | `void`             | Register a custom formatter for a type                            |

---

## Supported Types (Built-in)

LiteSerializer supports these types by default (no registration needed):

| Type Category         | Examples                                      |
|-----------------------|-----------------------------------------------|
| Primitive types       | `int`, `long`, `float`, `double`, `bool`, ... |
| Structs               | `DateTime`, `Guid`, `TimeSpan`, ...           |
| Nullable types        | `int?`, `DateTime?`, etc.                     |
| Arrays                | `int[]`, `Guid[]`, `DateTime[]`, ...          |
| Nullable arrays       | `int?[]`, `DateTime?[]`, ...                  |
| Enum types            | Any `enum`                                    |
| String types          | `string`, `string[]`                          |

> **Tip:** For custom classes/structs, implement `IFormatter<T>` and register using `LiteSerializer.Register<T>()`.

---

## Customizing Serialization Layout

You can control exactly how your type is serialized using a set of attributes provided by the library.  
This allows for precise over-the-wire layout, field selection, dynamic and fixed sizes, and version-safe extension.

### Key Attributes

| Attribute                             | Use for                                                                                                       |
|---------------------------------------|---------------------------------------------------------------------------------------------------------------|
| `[SerializePackable(layout)]`         | Mark class/struct/interface as serializable. `layout`: `Explicit` or `Sequential`                             |
| `[SerializeOrder(order)]`             | Specify explicit serialization order/field offset                                                             |
| `[SerializeDynamicSize(size)]`        | Mark a field/property as dynamic length (e.g. string, byte[]). Optional `size` hint for buffer preallocation. |
| `[SerializeIgnore]`                   | Field/property will be skipped during serialization                                                           |
| `IFixedSizeSerializable (interface)`  | For types that can always be serialized to the same fixed size (see below)                                    |

**Usage:**

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public class MyMessage
{
    [SerializeOrder(0)]
    public int Id { get; set; }

    [SerializeOrder(1)]
    public byte[] Data { get; set; }

    [SerializeOrder(2)]
    [SerializeDynamicSize] // Length can vary per instance!
    public string Note { get; set; }

    [SerializeIgnore]
    public string NonSerializedProp { get; set; }
}
```

- Use `SerializeOrder` to control on-wire order (critical for protocol compatibility).
- Use `SerializeDynamicSize` for fields where payload can vary in length.
- Use `SerializeIgnore` for transient/logic properties.

---

## Advanced: Layout Modes

- **Sequential (default):** Serializes fields/properties in definition order.
- **Explicit:** Uses explicit field order/offset, good for protocol and binary compatibility (especially useful with `[SerializeOrder]`).

```csharp
[SerializePackable(SerializeLayout.Sequential)]
public struct Foo { /* ... */ }

[SerializePackable(SerializeLayout.Explicit)]
public struct Bar { /* ... */ }
```

---

## Making Types Fixed-Size

To allow size calculation at compile time and maximize serialization performance (no per-instance checking),  
implement the interface:

```csharp
public interface IFixedSizeSerializable
{
    static abstract int Size { get; }
}
```

This is often recommended for protocol headers, GUID wrappers, or special struct types.

---

## Handling Special Values

Some constants are defined for special cases:

- **Null:** `SerializerBounds.Null` for null values
- **Max Array:** `SerializerBounds.MaxArray` is the maximum allowed array length
- **Max String:** `SerializerBounds.MaxString` for string field size
- **Null Array Marker:** A 4-byte marker `[255,255,255,255]` represents a null array
- **Empty Array Marker:** A 4-byte marker `[0,0,0,0]` for empty arrays

They are used by the framework for efficient wire signaling of special/null cases.

---

## Registering Custom Formatters

If you want full control or need custom logic/perf (e.g. for version-tolerant, compressed, partial serialization):

```csharp
LiteSerializer.Register<MyCustomType>(new MyCustomFormatter());
```

---

## Performance Tips

- For high-frequency operations, prefer `Span<byte>` and `BufferLease` overloads to avoid heap allocations.
- Use `BufferLease` for long-lived or pooled network/storage buffers.
- If you hit buffer size errors, check the minimum required buffer size for your type, or use the `byte[]`-returning overload for convenience.
- Always catch `SerializationException` for safety with unknown/heterogeneous payloads.

---

## Example: Serialize/Deserialize Struct

```csharp
struct Foo { public int X, Y; }
Foo f = new Foo { X = 10, Y = 20 };

// Fastest all-in-one
var bytes = LiteSerializer.Serialize(f);

Foo restored = default!;
LiteSerializer.Deserialize(bytes, ref restored);
// restored.X == 10
```

---

## Error Handling

- Throws `SerializationException` if buffer is too small, type unsupported, or data is malformed.
- All methods validate buffer bounds aggressively for safety and performance.

---

## License

Licensed under the Apache License, Version 2.0.
