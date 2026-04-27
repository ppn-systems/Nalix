# DataReader

`Nalix.Codec.Memory.DataReader` is a high-performance, allocation-free `ref struct` used for reading serialized data from various buffer types. 

It provides a unified reading abstraction over managed arrays, unmanaged memory pointers, spans, and memories, avoiding the need for multiple overloads in serialization code.

## Supported Data Sources

`DataReader` can be constructed around any of the following underlying types:

- `byte[]`: Managed arrays.
- `ReadOnlySpan<byte>`: Read-only spans.
- `ReadOnlyMemory<byte>`: Read-only memory chunks.
- `byte*`: Unmanaged raw pointers.

## Key Properties

- `BytesRead`: The number of bytes that have been consumed/advanced from the buffer.
- `BytesRemaining`: The number of bytes still available to be read.

## Reading Data

`DataReader` is designed to be fast and inlineable. It avoids maintaining complex state, relying simply on a reference to the buffer and a position index.

### 1. GetSpanReference

```csharp
public readonly ref byte GetSpanReference(int sizeHint)
```

Retrieves a reference to the first byte in the requested span, ensuring that at least `sizeHint` bytes are available. If `sizeHint` exceeds `BytesRemaining`, a `SerializationFailureException` is thrown.

This method allows for highly optimized, zero-copy reading using `Unsafe.ReadUnaligned` or similar memory operations.

### 2. Advance

```csharp
public void Advance(int count)
```

Advances the internal read position by `count` bytes. This is typically called immediately after reading data via the reference obtained from `GetSpanReference`.

## Example Usage

```csharp
public bool ReadBoolean(ref DataReader reader)
{
    // 1. Get reference to the next byte
    ref byte b = ref reader.GetSpanReference(sizeof(byte));
    
    // 2. Advance the reader position
    reader.Advance(sizeof(byte));
    
    // 3. Return the parsed value
    return b != 0;
}
```

!!! tip "Struct Nature"
    Because `DataReader` is a `ref struct`, it must be passed by `ref` to methods that need to consume data and advance the cursor. It cannot be stored on the heap or boxed.
