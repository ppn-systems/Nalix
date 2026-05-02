# BufferLease

`Nalix.Codec.Memory.BufferLease` is a core type in the Nalix ecosystem representing an owned lease of a pooled `byte[]` array. It is rented from the underlying `IBufferPoolManager` and supports advanced features like slice ownership, reference counting, and zero-allocation object pooling for the lease shells themselves.

## Core Concepts

### 1. Shell Pooling (Zero-Allocation)

To avoid Garbage Collection (GC) pressure from millions of lease allocations, `BufferLease` itself is pooled using a highly optimized, lock-free free-list (`ConcurrentStack` with atomic counting). When you call `BufferLease.Rent()`, you get a recycled shell object wrapped around a rented byte array. This makes the lease shell essentially allocation-free.

### 2. Slice Ownership & Zero-Copy Handoff

Unlike a standard `byte[]`, a `BufferLease` can represent a specific **slice** of an array (via `_start` and `Length`). This is critical for zero-copy handoffs where a payload is located after a protocol header in the same rented array. You can use `TakeOwnership` to create a lease over just that slice.

### 3. Reference Counting

`BufferLease` supports reference counting via `Retain()`. If multiple consumers need to hold the same buffer (e.g., broadcasting a message to multiple clients), they can `Retain()` the lease. The underlying byte array is only returned to the pool when the final owner calls `Dispose()`.

## API Overview

### Renting & Creating

- `Rent(int capacity, bool zeroOnDispose = false)`: Auto-rents a buffer and returns an empty slice. You write to `SpanFull` and then call `CommitLength()`.
- `CopyFrom(ReadOnlySpan<byte> src, bool zeroOnDispose = false)`: Creates a lease by copying source data into a newly rented buffer.
- `TakeOwnership(byte[] buffer, int start, int length, bool zeroOnDispose = false)`: Wraps a slice of an already rented array, taking ownership of it.
- `FromRented(byte[] buffer, int length)`: Wraps an entire rented array.

### Buffer Access

- `Span`: A read/write `Span<byte>` over the valid (committed) payload.
- `SpanFull`: A read/write `Span<byte>` over the entire available capacity.
- `Memory`: A `ReadOnlyMemory<byte>` over the valid payload.
- `Capacity`: Total capacity of the owned slice.
- `Length`: The committed length of the payload.

### Lifecycle Management

- `CommitLength(int length)`: Sets the valid payload length (used after writing to `SpanFull`).
- `Retain()`: Increments the reference count.
- `Dispose()`: Decrements the reference count and returns the buffer to the pool when it reaches zero.
- `ReleaseOwnership(out byte[]? buffer, out int start, out int length)`: Detaches the underlying array, transferring ownership to the caller. Returns `true` if the buffer was successfully detached; `false` if the lease has already been detached or has multiple references. Disposing the lease afterward does nothing.

## Example Usage

```csharp
// Rent a lease of 1024 bytes
using BufferLease lease = BufferLease.Rent(1024);

// Write data into the full capacity
int written = SerializeMyData(lease.SpanFull);

// Commit how much was actually written
lease.CommitLength(written);

// The payload is now accessible via lease.Span or lease.Memory
ProcessData(lease.Memory);

// The 'using' block automatically calls Dispose() to return the buffer.
```
