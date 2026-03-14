# Buffer Management

`Nalix.Framework.Memory` provides a high-performance byte buffer management system designed to minimize GC pressure and maximize throughput in networking hot paths.

## Buffer Rental & Disposal Lifecycle

The following diagram illustrates how raw byte arrays are managed through the `BufferLease` and `BufferPoolManager` abstraction.

```mermaid
sequenceDiagram
    participant App as Application Code
    participant Lease as IBufferLease
    participant Manager as BufferPoolManager
    participant OS as Managed Heap (ArrayPool)

    App->>Manager: Rent(size)
    Manager->>Manager: Find suitable bucket
    alt Bucket has free buffers
        Manager-->>App: Return byte[]
    else Bucket empty / No bucket
        Manager->>OS: Rent from ArrayPool.Shared
        OS-->>Manager: New byte[]
        Manager-->>App: Return byte[]
    end

    App->>Lease: Wrap byte[] in BufferLease
    App->>Lease: CommitLength(bytesWritten)
    
    rect rgb(240, 240, 240)
    Note over App, Lease: Business Logic (Using Span/Memory)
    end

    App->>Lease: Dispose()
    Lease->>Manager: Return(byte[])
    Manager->>Manager: Return to bucket
```

## Source Mapping

- `src/Nalix.Common/Abstractions/IBufferLease.cs`
- `src/Nalix.Framework/Memory/Buffers/BufferLease.cs`
- `src/Nalix.Framework/Memory/Buffers/BufferPoolManager.cs`
- `src/Nalix.Framework/Memory/Buffers/BufferConfig.cs`
- `src/Nalix.Framework/Memory/Buffers/BufferPoolState.cs`

## IBufferLease and BufferLease

`IBufferLease` is the primary interface for managing temporary byte storage. It encapsulates the ownership and lifetime of a pooled byte array.

### Core Features

- **Ownership Tracking**: Ensures buffers are returned to the correct pool upon disposal.
- **Span/Memory Integration**: High-performance access to the underlying byte array via `Span<byte>` or `Memory<byte>`.
- **Commit Pattern**: Allows marking only a portion of the rented buffer as "active data".
- **Zero-Allocation**: Designed to be used on the stack (as a `using` variable) to avoid any heap allocation during messaging.

### Key Members

| Member | Description |
| :--- | :--- |
| `Span` / `Memory` | Provides access to the *committed* portion of the buffer. |
| `SpanFull` | Provides access to the *entire* rented capacity. |
| `CommitLength(int)` | Sets the length of data actually written to the buffer. |
| `Retain()` | Increments the internal reference count (advanced use). |
| `ReleaseOwnership()` | Detaches the underlying array from the lease (transferring ownership). |

## BufferPoolManager

`BufferPoolManager` is the high-level orchestrator that organizes memory into size-aligned buckets (pools). It is typically registered as a singleton.

### Adaptive Trimming

The manager includes a background job that monitors pool utilization. It uses a **Shrink Safety Policy** to ensure that memory is returned to the OS during idle periods without causing churn when traffic spikes resume.

- **Trim Interval**: Frequency of routine memory checks.
- **Deep Trim**: A more aggressive cleanup cycle for long-term idle pools.
- **Safety Floor**: Always retains a minimum percentage of buffers to ensure readiness.

### Socket Excellence (SAEA Support)

Specialized APIs are provided to integrate directly with .NET's `SocketAsyncEventArgs`:

- `RentForSaea(saea, size)`: Rents a buffer and binds it to the SAEA.
- `ReturnFromSaea(saea)`: Unbinds and returns the buffer automatically.

## BufferConfig

Global tuning for the buffer system is managed via `BufferConfig`.

### Allocation Profiles

You can define the pool structure using the `BufferAllocations` string format: `size,ratio; size,ratio`.

```ini
BufferAllocations = 512,0.40; 2048,0.40; 8192,0.20
```

- **Size**: The maximum bytes this bucket can hold.
- **Ratio**: The percentage of `TotalBuffers` allocated to this bucket.

## Monitoring & Metrics

The `BufferPoolManager` provides deep insights into memory health:

- **MissRate**: If misses occur, it indicates the pool is too small or bucket sizes don't match your traffic.
- **UsageRatio**: Helps identify if you are over-provisioned or near capacity.

!!! tip "Reporting"
    Call `manager.GenerateReport()` to get a detailed text summary of all pool buckets and their current efficiency.

## Related APIs

- [Object Pooling](./object-pooling.md)
- [Network Options](../../network/options/options.md)
- [Zero-Allocation Path](../../../concepts/internals/zero-allocation.md)
