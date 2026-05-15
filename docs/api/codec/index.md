# Nalix.Codec API Reference

`Nalix.Codec` provides the serialization and packet registry infrastructure for the Nalix ecosystem.

## Serialization

Nalix uses a custom source-generated binary serializer optimized for zero-allocation performance.

- [**Serialization Basics**](./serialization/serialization-basics.md): Overview of the serialization engine.
- [**Packet Serialization**](./serialization/packet-serialization.md): How packets are transformed to and from wire format.
- [**Attributes**](../abstractions/serialization-attributes.md): Layout and behavior controls.
- [**IO & Headers**](./serialization/reader-writer-and-header-extensions.md): Reader/Writer primitives and header inspection.

## Packets & Registry

- [**Packet Registry**](./packets/packet-registry.md): Process-wide discovery and deserialization engine.
- [**Frame Model**](./packets/frame-model.md): The internal structure of Nalix frames.
- [**Built-in Frames**](./packets/built-in-frames.md): Signal packets for control and handshake.
- [**Fragmentation**](./packets/fragmentation.md): Handling of large payloads across multiple frames.

## Transforms

- [**LZ4**](./lz4.md): Integrated high-performance compression.
- [**Cryptography**](../security/cryptography.md): Framed packet encryption.

## Related Packages

- [Nalix.Abstractions](../abstractions/index.md)
- [Nalix.Environment](../environment/index.md)
- [Nalix.Framework](../framework/index.md)
- [Nalix.Network](../network/index.md)
- [Nalix.Runtime](../runtime/index.md)
