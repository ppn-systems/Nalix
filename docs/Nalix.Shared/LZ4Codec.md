# LZ4Codec — High-Performance Compression for .NET

**LZ4Codec** provides fast, allocation-minimal routines for compressing and decompressing data in the [LZ4](https://lz4.github.io/lz4/) format, with header and error handling suited for high-throughput streaming and networking scenarios.

- **Namespace:** `Nalix.Shared.LZ4`
- **Classes:** `LZ4Codec` (static), `LZ4BlockHeader` (struct)

---

## Features

- Ultra-fast streaming compression/decompression (LZ4 algorithm)
- Span/array overloads for allocation-free or heap-based workflows
- Header format includes original and compressed length (8 bytes)
- Full buffer/bounds safety and error reporting

---

## Quick Example

### Compress data

```csharp
using Nalix.Shared.LZ4;

byte[] input   = ...; // your raw data

// (1) Compress to new output buffer (recommended for simple use)
byte[] compressed = LZ4Codec.Encode(input);

// (2) Compress into your own buffer (zero allocation, use for performance-critical code)
Span<byte> outputBuffer = stackalloc byte[LZ4BlockEncoder.GetMaxLength(input.Length)];
int compressedLen = LZ4Codec.Encode(input, outputBuffer);

// outputBuffer[..compressedLen] is your valid compressed data
```

---

### Decompress data

```csharp
// (1) Decompress to new buffer
if (LZ4Codec.Decode(compressed, out var decompressed, out var written))
{
    // 'decompressed' contains your uncompressed data (length 'written')
}

// (2) Decompress into your own buffer
Span<byte> decompressedBuf = stackalloc byte[originalLength];
int actualWritten = LZ4Codec.Decode(compressed, decompressedBuf);
```

---

### Block Header Format

`LZ4BlockHeader` (8 bytes) is prepended to each compressed block:
    - `OriginalLength` (int32): Length of original uncompressed data
    - `CompressedLength` (int32): Length of compressed data, including header

You rarely manipulate this directly — handled for you by the codecs.

---

## API Summary

| Method                                                                  | Description                                              |
|-------------------------------------------------------------------------|----------------------------------------------------------|
| `Encode(ReadOnlySpan<byte> input, Span<byte> output)`                   | Compress into caller buffer, returns bytes written or -1 |
| `Encode(byte[] input, byte[] output)`                                   | Convenience overload                                     |
| `Encode(ReadOnlySpan<byte> input)`                                      | Compress to new byte[], returns trimmed array            |
| `Decode(ReadOnlySpan<byte> input, Span<byte> output)`                   | Decompress input into buffer, returns bytes written      |
| `Decode(byte[] input, byte[] output)`                                   | Decompress (array overload)                              |
| `Decode(ReadOnlySpan<byte> input, out byte[]? output, out int written)` | Allocates output, writes decompressed data               |

**Errors:**  

- Returns `-1` on (de)compression failure
- Throws on validation/memory access errors if input/output is invalid

---

## Best Practices

- For best performance, use the `Span<byte>` overloads to avoid extra allocations.
- Always use `LZ4BlockEncoder.GetMaxLength(inputLength)` to size your output buffer safely.
- When streaming, the decoder will expect `LZ4BlockHeader` to be present on each block.
- On decompressing, always provide a buffer at least as large as the original data.

---

## Example: In-place Compress + Decompress

```csharp
var raw = new byte[4096]; // ...fill with your data

var buffer = new byte[LZ4BlockEncoder.GetMaxLength(raw.Length)];
int n = LZ4Codec.Encode(raw, buffer);

// Extract valid portion for network/file
ReadOnlySpan<byte> compressedBlock = buffer.AsSpan(0, n);

// On the other side
var decompressed = new byte[raw.Length];
int decompLen = LZ4Codec.Decode(compressedBlock, decompressed);

// decompressed[..decompLen] now equals original
```

---

## LZ4BlockHeader

`LZ4BlockHeader` is an 8-byte structure automatically handled by the codec.  
You rarely (if ever) manipulate it directly.

| Field              | Type   | Description                                  |
|--------------------|--------|----------------------------------------------|
| `OriginalLength`   | int    | Size of original (pre-compressed) data       |
| `CompressedLength` | int    | Total size of compressed block (with header) |
| `Size`             | const  | Fixed value: 8                               |

---

## Error Handling

- Compression returns `-1` on insufficient output buffer or policy failure.
- Decompression returns `-1` or `false` when the input is truncated or corrupt.
- Exception thrown for access-violations; always catch in hostile environments.

---

## License

Licensed under the Apache License, Version 2.0.
