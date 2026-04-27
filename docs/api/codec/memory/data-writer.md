# DataWriter

`Nalix.Codec.Memory.DataWriter` is a mutable, growable, high-performance write buffer implemented as a `ref struct`. It is specifically designed to handle high-throughput serialization with minimal overhead.

## Buffer Modes

`DataWriter` can operate in two distinct modes depending on how it is constructed:

1. **Rented Mode**: Constructed via `new DataWriter(int size)`. The writer internally rents a `byte[]` from `BufferLease.ByteArrayPool`. In this mode, the writer can dynamically `Expand()` if it runs out of space, automatically renting a larger array and copying data over. Calling `Dispose()` returns the rented array to the pool.
2. **Fixed Mode**: Constructed via `new DataWriter(byte[] buffer)` or `new DataWriter(Span<byte> span)`. The writer wraps an external, fixed-size buffer. In this mode, calling `Expand()` when there is insufficient space will throw a `SerializationFailureException`, and `Dispose()` performs no pooling actions.

## Key Properties

- `WrittenCount`: The number of bytes successfully committed to the buffer.
- `FreeBuffer`: A `Span<byte>` representing the remaining, unwritten segment of the buffer.

## Writing Data

### 1. Expand (Ensuring Capacity)

```csharp
public void Expand(int minimumSize)
```

Ensures that at least `minimumSize` bytes are available in the `FreeBuffer`. If operating in Rented Mode and capacity is insufficient, it will rent a new buffer (typically doubling the current size) and copy existing written bytes into the new array.

### 2. GetFreeBufferReference

```csharp
public readonly ref byte GetFreeBufferReference()
```

Returns a reference to the first byte of the free space. Always call `Expand()` before retrieving this reference to guarantee safety.

### 3. Advance

```csharp
public void Advance(int count)
```

Advances the write cursor by `count` bytes, effectively committing them to the `WrittenCount`.

## Finalizing

- `ToArray()`: Copies the committed data into a brand new, tightly-sized `byte[]`.
- `Dispose()`: Clears the state and, if in Rented Mode, returns the underlying array to the pool.

## Example Usage

```csharp
public void WriteInt32(ref DataWriter writer, int value)
{
    // 1. Ensure enough space is available
    writer.Expand(sizeof(int));
    
    // 2. Write directly to the buffer reference
    Unsafe.WriteUnaligned(ref writer.GetFreeBufferReference(), value);
    
    // 3. Advance the cursor
    writer.Advance(sizeof(int));
}
```

!!! warning "Disposal"
    Always use a `using` block or manually call `Dispose()` on `DataWriter` when operating in Rented Mode to prevent memory leaks from unreturned pooled arrays.
