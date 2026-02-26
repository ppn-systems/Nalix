# Frame Model

This page covers the core `Nalix.Framework.DataFrames` abstractions that sit underneath built-in frames and custom packet types.

## Source mapping

- `src/Nalix.Framework/DataFrames/FrameBase.cs`
- `src/Nalix.Framework/DataFrames/PacketBase.cs`
- `src/Nalix.Framework/DataFrames/FrameTransformer.cs`

## Main types

- `FrameBase`
- `PacketBase<TSelf>`
- `FrameTransformer`

## Public members at a glance

| Type | Public members |
|---|---|
| `FrameBase` | `MagicNumber`, `OpCode`, `Flags`, `Priority`, `Protocol`, `SequenceId`, `Length`, `Serialize()`, `Serialize(Span<byte>)`, `ResetForPool()` |
| `PacketBase<TSelf>` | frame members plus `GenerateReport()`, `GetReportData()`, `Deserialize(ReadOnlySpan<byte>)` |
| `FrameTransformer` | `Encrypt`, `TryEncrypt`, `Decrypt`, `TryDecrypt`, `Compress`, `TryCompress`, `Decompress`, `TryDecompress`, size helpers |

## FrameBase

`FrameBase` is the common wire-level header contract for Nalix packets.

It exposes the fields that every packet carries:

- `MagicNumber`
- `OpCode`
- `Flags`
- `Priority`
- `Protocol`
- `SequenceId`
- `Length`

It also defines the common packet lifecycle methods:

- `Serialize()`
- `Serialize(Span<byte>)`
- `ResetForPool()`

Use `FrameBase` when you want to understand the shared header layout or build infrastructure that only cares about `IPacket`-level metadata.

## PacketBase<TSelf>

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
[SerializePackable(SerializeLayout.Explicit)]
public sealed class ChatMessage : PacketBase<ChatMessage>
{
    [SerializeDynamicSize(256)]
    [SerializeOrder(PacketHeaderOffset.Region)]
    public string Content { get; set; } = string.Empty;

    public void Initialize(string content) => Content = content ?? string.Empty;
}
```

### Notes from source

- `MagicNumber` is restored automatically during `ResetForPool()`, so pooled packets keep stable type identity.
- fixed-size packets get a cached `Length`; dynamic fields such as `string` and `byte[]` are measured at runtime.
- static `Deserialize(ReadOnlySpan<byte>)` throws on empty or malformed input instead of silently returning a partial packet.

## FrameTransformer

`FrameTransformer` applies payload-level transforms while preserving the packet header region.

It works on the bytes after `PacketHeaderOffset.Region`, leaving the frame header untouched.

### Common operations

- `Encrypt(...)` / `TryEncrypt(...)`
- `Decrypt(...)` / `TryDecrypt(...)`
- `Compress(...)` / `TryCompress(...)`
- `Decompress(...)` / `TryDecompress(...)`
- `GetMaxCiphertextSize(...)`
- `GetPlaintextLength(...)`
- `GetMaxCompressedSize(...)`
- `GetDecompressedLength(...)`

## Basic usage

```csharp
int maxCipher = FrameTransformer.GetMaxCiphertextSize(
    CipherSuiteType.CHACHA20_POLY1305,
    plaintextSize: payloadSize);

bool encrypted = FrameTransformer.Encrypt(sourceLease, destLease, key, CipherSuiteType.CHACHA20_POLY1305);
```

### Operational model

- header bytes are copied through unchanged
- encryption uses `EnvelopeCipher`
- compression uses pooled `LZ4Codec`
- `Try*` overloads still catch failures and return `false`
- non-`Try*` overloads now raise exceptions for invalid input, malformed envelopes, or transform failures

### Common pitfalls

- expecting `ResetForPool()` to preserve custom runtime state
- using `FrameTransformer` on bytes that still include the header region
- assuming `Deserialize(...)` will silently accept malformed input

## When to use which layer

| Need | Start with |
|---|---|
| Define a new packet type | `PacketBase<TSelf>` |
| Inspect common packet header fields | `FrameBase` |
| Encrypt or compress packet payload bytes | `FrameTransformer` |
| Discover or deserialize packets at runtime | [Packet Registry](./packet-registry.md) |

## Related APIs

- [Packet Registry](./packet-registry.md)
- [Built-in Frames](./built-in-frames.md)
- [Fragmentation](./fragmentation.md)
- [Serialization](./serialization.md)
- [Buffer and Pooling](../memory/buffer-and-pooling.md)
