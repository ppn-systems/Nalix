# LZ4

This page covers the public compression surface in `Nalix.Framework.LZ4`.

## Source mapping

- `src/Nalix.Framework/LZ4/LZ4Codec.cs`
- `src/Nalix.Framework/LZ4/LZ4BlockHeader.cs`
- `src/Nalix.Framework/LZ4/Encoders/*`
- `src/Nalix.Framework/LZ4/Engine/*`

## Main types

- `LZ4Codec`
- `LZ4BlockHeader`

## What it does

This layer provides:

- block compression into a caller-supplied span
- block compression into a pooled `BufferLease`
- block decompression into a caller-supplied span
- block decompression into a rented `BufferLease`

## Runtime shape

```mermaid
flowchart LR
    A["ReadOnlySpan<byte> input"] --> B["LZ4Codec"]
    B --> C["Span<byte> output"]
    B --> D["BufferLease output"]
```

## Block format

Compressed payloads include an `LZ4BlockHeader` before the body.

The header stores:

- `OriginalLength`
- `CompressedLength`

`LZ4BlockHeader.Size` is currently `8` bytes.

## LZ4CompressionConstants

`LZ4CompressionConstants` exposes the implementation-level constants used by the encoder.

## Source mapping

- `src/Nalix.Framework/LZ4/Encoders/LZ4CompressionConstants.cs`

Important values include:

- `MinMatchLength`
- `MaxOffset`
- `MaxBlockSize`
- `LastLiteralSize`
- `TokenMatchMask`
- `TokenLiteralMask`

Two of these matter most when reasoning about behavior:

- `MaxOffset` is the hard backward-reference limit defined by the LZ4 format
- `MaxBlockSize` is the implementation cap chosen by Nalix for safety and practicality, not a protocol-wide LZ4 hard limit

## Encode overloads

`LZ4Codec.Encode(...)` supports:

- `Encode(ReadOnlySpan<byte> input, Span<byte> output)`
- `Encode(ReadOnlySpan<byte> input, out BufferLease lease, out int bytesWritten)`

Use the span overload when you already own the destination buffer.

Use the `BufferLease` overload when you want a pooled, zero-copy-friendly path.

## Decode overloads

`LZ4Codec.Decode(...)` supports:

- `Decode(ReadOnlySpan<byte> input, Span<byte> output)`
- `Decode(ReadOnlySpan<byte> input, out BufferLease? lease, out int bytesWritten)`

Use the `BufferLease` overload on hot paths where you want pooled output.

## Example

```csharp
ReadOnlySpan<byte> input = payload;

LZ4Codec.Encode(input, out BufferLease compressed, out int written);
using (compressed)
{
    LZ4Codec.Decode(compressed.Span, out BufferLease? restored, out int restoredBytes);
    using (restored)
    {
        Console.WriteLine(restoredBytes);
    }
}
```

## Important notes

!!! tip "Prefer pooled paths on hot routes"
    The `BufferLease` overloads are the best default when compression is part of a high-throughput network path.

!!! note "Failures now throw"
    The public LZ4 APIs no longer use boolean success/failure flow. Invalid buffers, malformed payloads, and unexpected codec failures surface as exceptions, while pooled leases are disposed on failing paths.

## Related APIs

- [Buffer and Pooling](./buffer-and-pooling.md)
- [Serialization](../packets/serialization.md)
- [Compression Options](../../network/options/compression-options.md)
