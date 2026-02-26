# Serialization Attributes

`Nalix.Common.Serialization` contains the low-level attributes and helpers that shape how packets and models are serialized.

Use this page when you need to understand the attribute layer before you work with `PacketBase<TSelf>` or `LiteSerializer`.

## Source mapping

- `src/Nalix.Common/Serialization/SerializePackableAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeOrderAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeIgnoreAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeHeaderAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeDynamicSizeAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeLayout.cs`
- `src/Nalix.Common/Serialization/SerializerBounds.cs`
- `src/Nalix.Common/Serialization/IFixedSizeSerializable.cs`

## Main types

- `SerializePackableAttribute`
- `SerializeOrderAttribute`
- `SerializeIgnoreAttribute`
- `SerializeHeaderAttribute`
- `SerializeDynamicSizeAttribute`
- `SerializeLayout`
- `SerializerBounds`
- `IFixedSizeSerializable`

## What this layer does

These types define how Nalix interprets a serializable model:

- whether the type is packable
- which members are serialized and in what order
- which members are ignored
- which members belong in the packet header region
- which members use dynamic sizing
- which shapes are considered fixed-size

## Important behavior

The attribute layer allows the core serialization attributes to be placed on either a field or a property.

For `LiteSerializer` automatic object and struct serialization, the effective runtime behavior is still field-based:

- fields are serialized directly
- properties mainly act as metadata carriers
- auto-properties usually work because their attributes can be associated with the generated backing field
- custom properties without a compiler-generated backing field are not serialized as independent members
- static members are outside the automatic serialization model

## Attributes

### `SerializePackableAttribute`

Marks a type as packable and tells the serializer to treat it as an explicitly supported wire model.

Common use:

- packet models
- value objects that should serialize without a nullable wrapper path

### `SerializeOrderAttribute`

Controls the order of serialized members.

It is useful when:

- wire order must stay stable
- you need explicit header/body layout
- a type has multiple fields or auto-properties that must serialize in a defined sequence

### `SerializeIgnoreAttribute`

Excludes a member from serialization.

Use it for:

- cached values
- computed properties
- runtime-only state

On automatic serializers, this is especially useful on properties that expose derived state but should not participate in field discovery through a backing field.

### `SerializeHeaderAttribute`

Marks a member as part of the header region.

This is mainly useful when your type has a strict packet header/body split.

### `SerializeDynamicSizeAttribute`

Declares that a member uses a runtime size instead of a fixed compile-time size.

This matters for fields such as:

- `string`
- `byte[]`
- other variable-length payload segments

## Layout and bounds

### `SerializeLayout`

`SerializeLayout` controls whether a type is treated as explicit or automatic during serialization.

### `SerializerBounds`

`SerializerBounds` contains the internal size limits and helper constants used by the serialization layer.

It exists so serializers can make consistent decisions about:

- maximum element sizes
- header offsets
- safe length calculations

## `IFixedSizeSerializable`

`IFixedSizeSerializable` is the marker contract for types that can report a stable fixed serialized size.

Use it when:

- the serialized layout never changes length
- you want the serializer to skip dynamic-size handling

## Example

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class PingRequest
{
    [SerializeOrder(0)]
    public ushort Opcode { get; set; }

    [SerializeDynamicSize(64)]
    [SerializeOrder(1)]
    public string Message { get; set; } = string.Empty;
}
```

For automatic member discovery, prefer straightforward fields or auto-properties for members that carry wire data.
If a model is immutable by constructor design or depends on strict readonly semantics, use a custom formatter rather than assuming every property shape can be reconstructed automatically.

## Relationship to `PacketBase<TSelf>`

Most packet types use these attributes together with `PacketBase<TSelf>`.

The typical flow is:

1. declare the packet model
2. add serialization attributes
3. let the framework build the serializer or formatter
4. send the packet through the runtime

## Related APIs

- [Serialization](../framework/packets/serialization.md)
- [Frame Model](../framework/packets/frame-model.md)
- [Packet Contracts](./packet-contracts.md)
