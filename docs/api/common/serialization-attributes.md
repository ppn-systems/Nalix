# Serialization Attributes

# Serialization Attributes

`Nalix.Common.Serialization` contains the low-level attributes and helpers that shape how packets and models are serialized.

Use this page when you need to understand the attribute layer before you work with `PacketBase<TSelf>` or `LiteSerializer`.

## Source mapping

- `src/Nalix.Common/Networking/Packets/PacketAttribute.cs`
- `src/Nalix.Common/Serialization/SerializePackableAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeOrderAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeIgnoreAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeHeaderAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeDynamicSizeAttribute.cs`
- `src/Nalix.Common/Serialization/SerializeLayout.cs`
- `src/Nalix.Common/Serialization/SerializerBounds.cs`
- `src/Nalix.Common/Serialization/IFixedSizeSerializable.cs`

## Main types

- `PacketAttribute` (aliased as `[Packet]`)
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

### `PacketAttribute` (or `[Packet]`)

Marks a class for automatic discovery and registration. It allows the `PacketRegistryFactory` to find and bind the type to its deserializer at runtime without explicit manual registration.

### `SerializePackableAttribute`

Marks a type as packable and tells the serializer to treat it as an explicitly supported wire model.

Common use:

- packet models
- value objects that should serialize without a nullable wrapper path

### `SerializeOrderAttribute`

Controls the order of serialized members.

Important: `SerializeOrder` is an ordering key, not a byte offset.  
`[SerializeOrder(10)]` means "after 9", not "start at byte 10".

It is useful when:
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
