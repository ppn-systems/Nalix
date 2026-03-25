# Serialization Basics

`Nalix.Framework.Serialization` provides the serializer entry point and formatter resolution layer used by the framework and packet system.

Use this page when you want the lower-level model behind `LiteSerializer` and `FormatterProvider`.

## Source mapping

- `src/Nalix.Framework/Serialization/IFormatter.cs`
- `src/Nalix.Framework/Serialization/FormatterProvider.cs`
- `src/Nalix.Framework/Serialization/LiteSerializer.cs`
- `src/Nalix.Framework/Serialization/Formatters/Primitives`
- `src/Nalix.Framework/Serialization/Formatters/Collections`
- `src/Nalix.Framework/Serialization/Formatters/Automatic`

## Main types

- `IFormatter<T>`
- `IFillableFormatter<T>`
- `FormatterProvider`
- `LiteSerializer`
- built-in primitive, enum, collection, memory, tuple, object, and struct formatters

## What this layer does

The framework serialization layer is responsible for:

- resolving the right formatter for a type
- serializing and deserializing values to `byte[]`, `Span<byte>`, `ReadOnlySpan<byte>`, and `ReadOnlyMemory<byte>` surfaces
- supporting built-in primitive and collection shapes
- generating formatters for supported object and struct types
- rehydrating existing instances when a formatter implements `IFillableFormatter<T>`

## `LiteSerializer`

`LiteSerializer` is the main convenience API. Use it when you want to serialize or deserialize a supported model without working directly with formatter instances.

### Public overload groups

| Operation | Overload shape | Notes |
|---|---|---|
| Serialize to owned array | `Serialize<T>(in T value)` | Allocates a right-sized `byte[]`; unmanaged values use direct unaligned writes. |
| Serialize to caller array | `Serialize<T>(in T value, byte[] buffer)` | Writes into the provided array and returns bytes written. |
| Serialize to caller span | `Serialize<T>(in T value, Span<byte> buffer)` | Zero-copy caller-owned path. Span-backed writers cannot grow. |
| Deserialize into existing value | `Deserialize<T>(byte[]/ReadOnlyMemory<byte>/ReadOnlySpan<byte>, ref T value)` | Uses `IFillableFormatter<T>.Fill` when available and `value` is non-null. |
| Deserialize new value | `Deserialize<T>(byte[]/ReadOnlyMemory<byte>/ReadOnlySpan<byte>, out int bytesRead)` | Creates or returns a value and reports consumed bytes. |
| Register custom formatter | `Register<T>(IFormatter<T> formatter)` | Delegates to `FormatterProvider.Register`. |

### Example

```csharp
byte[] bytes = LiteSerializer.Serialize(model);
MyModel clone = LiteSerializer.Deserialize<MyModel>(bytes, out int bytesRead);

Span<byte> scratch = stackalloc byte[256];
int written = LiteSerializer.Serialize(model, scratch);
```

## `FormatterProvider`

`FormatterProvider` resolves a formatter for a specific type.

Use it when you want lower-level control than `LiteSerializer` gives you.

```csharp
IFormatter<MyModel> formatter = FormatterProvider.Get<MyModel>();
```

Register custom formatters before relying on the automatic fallback path:

```csharp
LiteSerializer.Register(new MyCustomFormatter());
```

That lets the serializer pick your formatter before falling back to the built-in resolution path.

## Formatter contracts

`IFormatter<T>` is the formatter contract behind the serializer system. It defines the serialize and deserialize behavior for a specific type.

```csharp
public interface IFormatter<T>
{
    void Serialize(ref DataWriter writer, T value);
    T Deserialize(ref DataReader reader);
}
```

`IFillableFormatter<T>` extends the contract with in-place rehydration:

```csharp
public interface IFillableFormatter<T> : IFormatter<T>
{
    void Fill(ref DataReader reader, T value);
}
```

When `LiteSerializer.Deserialize(..., ref value)` receives a non-null value and the root formatter is fillable, the serializer calls `Fill` instead of replacing the instance. This is important for pooled packet objects and other long-lived containers.

## Supported shapes

The current source supports these groups directly:

- unmanaged primitives and value types
- `string` and `string[]`
- nullable value types such as `int?`, `Guid?`, `DateTime?`
- unmanaged arrays such as `int[]`, `Guid[]`, `DateTime[]`
- nullable arrays such as `int?[]`, `Guid?[]`
- enum values, enum arrays, and enum lists
- `List<T>`
- `Dictionary<TKey, TValue>`
- `Queue<T>`
- `Stack<T>`
- `HashSet<T>`
- `Memory<T>` and `ReadOnlyMemory<T>` for unmanaged element types
- `ValueTuple` arity 2 through 5
- automatic class and struct serialization through generated formatters

## Encoding and bounds rules

| Shape | Encoding behavior | Guardrail |
|---|---|---|
| Unmanaged value | Raw unaligned bytes. | Reader requires at least `sizeof(T)` bytes. |
| Unmanaged array | 4-byte length prefix followed by raw element bytes. | Length must be within `SerializerBounds.MaxArray`; total byte size must not overflow. |
| Null unmanaged array | 4-byte null marker. | Marker is read before element allocation. |
| Empty unmanaged array | 4-byte zero marker. | Round-trips as an empty array. |
| `string` | 4-byte UTF-8 byte-count prefix followed by UTF-8 payload. | Byte count must not exceed `SerializerBounds.MaxString`. |
| Null `string` | Dedicated null sentinel. | Null and empty strings remain distinct. |
| Enum | Backing integral type. | Supported backing types are byte, sbyte, short, ushort, int, uint, long, and ulong. |

Span-backed serialization wraps the caller buffer directly. If the formatter writes past the span capacity, the writer cannot expand and the operation fails instead of allocating a replacement buffer.

## Built-in formatter details

### `StringFormatter`

`StringFormatter` writes a 32-bit UTF-8 byte length, then encodes directly into the writer's free buffer. It handles three distinct cases:

- `null` writes the serializer null sentinel
- `string.Empty` writes length `0` with no payload
- non-empty strings write a positive byte count plus UTF-8 bytes

During reads, negative values other than the null sentinel and lengths greater than `SerializerBounds.MaxString` are rejected.

### `EnumFormatter<T>`

`EnumFormatter<T>` resolves the enum backing type once in its static constructor and caches specialized serialize/deserialize delegates. The hot path reinterprets enum bits with `Unsafe.As` and then delegates to the matching primitive formatter, avoiding boxing.

Supported backing types are:

- `byte` / `sbyte`
- `short` / `ushort`
- `int` / `uint`
- `long` / `ulong`

## Automatic member model

Generated object and struct formatters are field-based.

- instance fields are serialized directly
- public and non-public instance fields participate in discovery
- static members do not participate
- properties are not invoked directly as serialization accessors
- auto-properties typically work through their compiler-generated backing fields
- custom or computed properties should be treated as non-serializable unless you provide a custom formatter

This means a shape like `public int Count { get; set; }` is a good fit for automatic serialization, while a shape like `public int Count => _items.Count;` is metadata only and should usually be ignored.

Readonly and constructor-only models need extra care. If a type depends on strict immutability semantics, the recommended approach is to register a custom `IFormatter<T>` that reconstructs the object intentionally.

## Relationship to packet serialization

This layer sits underneath packet model serialization and frame handling.

It is the machinery that `PacketBase<TSelf>` and related frame helpers build on.

## Related APIs

- [Serialization Concept](../../../concepts/fundamentals/packet-system.md)
- [Custom Serialization Provider Guide](../../../guides/extensibility/serialization-providers.md)
