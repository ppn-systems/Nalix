# Frame Model

This page covers the core `Nalix.Codec.DataFrames` abstractions that sit underneath built-in frames and custom packet types.

## Source mapping

- `src/Nalix.Codec/DataFrames/FrameBase.cs`
- `src/Nalix.Codec/DataFrames/PacketBase.cs`
- `src/Nalix.Codec/Transforms/FrameTransformer.cs`
- `src/Nalix.Codec/Transforms/FrameCipher.cs`
- `src/Nalix.Codec/Transforms/FrameCompression.cs`

## Main types

- `FrameBase`
- `PacketBase<TSelf>`
- `FrameTransformer`

## Public members at a glance

| Type | Public members |
| --- | --- |
| `FrameBase` | `MagicNumber`, `OpCode`, `Flags`, `Priority`, `SequenceId`, `Length`, `Serialize()`, `Serialize(Span<byte>)`, `ResetForPool()` |
| `PacketBase<TSelf>` | frame members plus `GenerateReport()`, `GetReportData()`, `Deserialize(ReadOnlySpan<byte>)`, `Deserialize(ReadOnlySpan<byte>, ref TSelf)` |
| `FrameTransformer` | low-level payload transform helpers and size calculations |
| `FrameCipher` | shared framed packet encrypt/decrypt helper |
| `FrameCompression` | shared framed packet compress/decompress helper |

## FrameBase

`FrameBase` is the common wire-level header contract for Nalix packets.

It exposes the fields that every packet carries:

- `MagicNumber`
- `OpCode`
- `Flags`
- `Priority`
- `SequenceId`
- `Length`

It also defines the common packet lifecycle methods:

- `Serialize()`
- `Serialize(Span<byte>)`
- `ResetForPool()`

Use `FrameBase` when you want to understand the shared header layout or build infrastructure that only cares about `IPacket`-level metadata.

## PacketBase<`TSelf`>

`PacketBase<TSelf>` is the usual base class for real packet implementations.

It adds the behavior most application packets want by default:

- automatic `MagicNumber` generation from the concrete type name through `PacketRegistryFactory.Compute(...)`
- cached reflection metadata for ordered serializable properties
- automatic `Length` calculation for fixed-size and dynamic-size payloads
- `LiteSerializer`-based `Serialize(...)` and `Deserialize(...)`
- pooled reset behavior that restores header defaults and packet fields
- diagnostics through `GenerateReport()` and `GetReportData()`

## Basic usage

```csharp
using Nalix.Codec.DataFrames;
using Nalix.Codec.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ChatMessage : PacketBase<ChatMessage>
{
    [SerializeDynamicSize(256)]
    [SerializeOrder(0)]
    public string Content { get; set; } = string.Empty;

    public void Initialize(string content) => Content = content ?? string.Empty;
}
```

## FrameTransformer

`FrameTransformer` applies payload-level transforms while preserving the packet header region.

It works on the bytes after `FrameTransformer.Offset`, leaving the frame header untouched.
The shared `FrameCipher` and `FrameCompression` helpers build on top of this layer for full framed packet flows.

For higher-level frame workflows, prefer the shared helpers:

- `FrameCipher`
- `FrameCompression`

### Common operations

- `FrameCipher.EncryptFrame(...)`
- `FrameCipher.DecryptFrame(...)`
- `FrameCompression.CompressFrame(...)`
- `FrameCompression.DecompressFrame(...)`
- `FrameTransformer.GetMaxCiphertextSize(...)`
- `FrameTransformer.GetPlaintextLength(...)`
- `FrameTransformer.GetMaxCompressedSize(...)`
- `FrameTransformer.GetDecompressedLength(...)`

## When to use which layer

| Need | Start with |
| --- | --- |
| Define a new packet type | `PacketBase<TSelf>` |
| Inspect common packet header fields | `FrameBase` |
| Encrypt or compress a full framed packet | `FrameCipher` / `FrameCompression` |
| Work on raw payload transforms directly | `FrameTransformer` |
| Discover or deserialize packets at runtime | [Packet Registry](./packet-registry.md) |

## Related APIs

- [Packet Registry](./packet-registry.md)
- [Built-in Frames](./built-in-frames.md)
- [Fragmentation](./fragmentation.md)
- [Serialization](../serialization/serialization-basics.md)
- [Buffer Management](../../framework/memory/buffer-management.md)
- [Object Pooling](../../framework/memory/object-pooling.md)
