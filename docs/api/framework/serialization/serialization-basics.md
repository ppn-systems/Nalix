# Serialization Basics

`Nalix.Framework.Serialization` provides the serializer entry point and formatter resolution layer used by the framework and packet system.

Use this page when you want the lower-level model behind `LiteSerializer` and `FormatterProvider`.

## Source mapping

- `src/Nalix.Framework/Serialization/IFormatter.cs`
- `src/Nalix.Framework/Serialization/FormatterProvider.cs`
- `src/Nalix.Framework/Serialization/LiteSerializer.cs`
- `src/Nalix.Framework/Serialization/Formatters/Primitives/*`
- `src/Nalix.Framework/Serialization/Formatters/Collections/*`
- `src/Nalix.Framework/Serialization/Formatters/Automatic/*`

## Main types

- `IFormatter<T>`
- `FormatterProvider`
- `LiteSerializer`

## What this layer does

The framework serialization layer is responsible for:

- resolving the right formatter for a type
- serializing and deserializing values
- supporting built-in primitive and collection shapes
- generating formatters for supported object and struct types

## `LiteSerializer`

`LiteSerializer` is the main convenience API.

Use it when you want to serialize or deserialize a supported model without working directly with formatter instances.

### Example

```csharp
byte[] bytes = LiteSerializer.Serialize(model);
MyModel clone = LiteSerializer.Deserialize<MyModel>(bytes, out int bytesRead);
```

## `FormatterProvider`

`FormatterProvider` resolves a formatter for a specific type.

Use it when you want lower-level control than `LiteSerializer` gives you.

### Example

```csharp
IFormatter<MyModel> formatter = FormatterProvider.Get<MyModel>();
```

## `IFormatter<T>`

`IFormatter<T>` is the formatter contract behind the serializer system.

It defines the serialize and deserialize behavior for a specific type.

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

## Overriding behavior

If you need custom behavior for one type, register your formatter first:

```csharp
LiteSerializer.Register(new MyCustomFormatter());
```

That lets the serializer pick your formatter before falling back to the built-in resolution path.

## Relationship to packet serialization

This layer sits underneath packet model serialization and frame handling.

It is the machinery that `PacketBase<TSelf>` and related frame helpers build on.

## Related APIs

- [Serialization](../../framework/packets/serialization.md)
- [Frame Model](../../framework/packets/frame-model.md)
- [Serialization Attributes](../../common/serialization-attributes.md)
