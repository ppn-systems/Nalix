# BufferLease — Zero-Copy, Pooled Buffer Ownership for High-Performance .NET

**BufferLease** is a lightweight, pooled buffer manager that provides slice-based, ref-counted "leases" on rented `byte[]` arrays, enabling high-performance, zero-copy workflows for networking, serialization, memory IO, encryption, and more.

- **Namespace:** `Nalix.Shared.Memory.Buffers`
- **Class:** `BufferLease` (sealed)
- **Pattern:** Rent/return pattern, zero-allocation, multi-owner, detachable slices.

---

## Features

- Rent pooled buffers, track valid [start..start+length) slice
- Zero-copy handoff: detach or "take ownership" to avoid copying
- Explicit buffer zeroing/disposal for security (optional, on dispose)
- Supports reference-counted multiple owners (Retain, Dispose)
- Slicing and in-place edits via Span/Memory API
- All pooling handled automatically (safe for multi-threaded use)

---

## Typical Usage

### Rent and Use a Buffer

```csharp
using Nalix.Shared.Memory.Buffers;
using var lease = BufferLease.Rent(1024);
// lease.SpanFull gives you the writable buffer (initially empty)
lease.SpanFull[..4].CopyFrom(headerData);    // Write header
var payloadLength = 512;
lease.SpanFull[4..(4+payloadLength)].CopyFrom(payload);
lease.CommitLength(4 + payloadLength);
// pass 'lease' to consumer or encode/send lease.Span[..lease.Length]
```

---

### Ensure Automatic Pooling

- **Dispose** must be called to return a buffer to the pool (or use `using`).
- Set `ZeroOnDispose=true` to clear sensitive data before releasing memory.

```csharp
using var lease = BufferLease.Rent(4096, zeroOnDispose: true);
// ... Use lease.SpanFull ...
// lease will be zeroed and returned to pool automatically
```

---

### Copy From Source Without Manual Allocation

```csharp
ReadOnlySpan<byte> src = ...;
using var lease = BufferLease.CopyFrom(src);
// lease now owns the buffer, lease.Length == src.Length
```

---

### Zero-Copy Handoff (Slice Adoption)

If you want to "adopt" part of a larger pooled buffer (e.g., after protocol parsing):

```csharp
var baseBuffer = ...; // usually from pool
int start = headerLen, length = payloadLen;
var subLease = BufferLease.TakeOwnership(baseBuffer, start, length);
// subLease is now an isolated owner of the slice (no extra copying done)
```

---

### Detach and Take Ownership

You can transfer ownership of a buffer out of a lease (avoiding pool return) — useful when sending to legacy APIs or native code:

```csharp
if (lease.ReleaseOwnership(out var arr, out var offset, out var len))
{
    // 'arr', 'offset', and 'len' now represent the buffer and range
    // Caller is now responsible for 'arr'
}
```

---

## API Overview

| Method                                                       | Purpose / Returns                                  |
|--------------------------------------------------------------|----------------------------------------------------|
| `BufferLease.Rent(capacity, zeroOnDispose)`                  | Rent zeroed (optionally) buffer of desired size    |
| `BufferLease.CopyFrom(ReadOnlySpan<byte>)`                   | Copy data into fresh pooled buffer lease           |
| `BufferLease.FromRented(byte[], len, zeroOnDispose)`         | Wrap rented buffer as a lease                      |
| `BufferLease.TakeOwnership(arr, start, len, zeroOnDispose)`  | Wrap a slice of an array as lease                  |
| `ReleaseOwnership(out arr, out ofs, out len)`                | Transfer ownership to caller, disables pooling     |
| `Retain()`                                                   | Increment ref count (multi-owner)                  |
| `Dispose()`                                                  | Decrement ref count, auto-returns to pool when zero|
| `CommitLength(n)`                                            | Set/adjust valid payload length                    |
| `Span` / `SpanFull`                                          | Get read/write window over data/slice/full buf     |
| `Memory`                                                     | Get `ReadOnlyMemory<byte>` of valid data           |

---

## Notes & Best Practices

- Always `Dispose()` or use `using` to return pooled buffers
- For secure scenarios (cryptography, tokens), set `ZeroOnDispose = true`
- Use `Retain()` if passing to multiple consumers — ensures buffer is only returned to pool when all are done
- `ReleaseOwnership()` prevents pool release and gives raw array to caller — only use if you know what you're doing!
- Use `CommitLength()` after writing new data to update payload length if needed

---

## Example: Reference-counted Sharing

```csharp
var lease = BufferLease.Rent(256);
// . . . some consumer:
lease.Retain();
DoAsyncWork(lease);

void DoAsyncWork(BufferLease shared) {
    try {
        // use shared.Memory or shared.Span
    } finally {
        shared.Dispose(); // decrement ref count
    }
}
// Main/downstream code also calls .Dispose()
```

---

## Error Handling

- Throws `ObjectDisposedException` if using a lease after release/dispose.
- Throws `InvalidOperationException` on ref-count mismatch or improper detachment.
- Always check method contract for bounds.

---

## License

Licensed under the Apache License, Version 2.0.
