# Memory & Storage Benchmarks

Nalix uses a highly optimized memory management subsystem designed to eliminate Garbage Collection (GC) pauses in high-throughput workloads.

## Buffer Pooling & Leases

Comparison of memory acquisition strategies across various sizes (64 B, 1 KB, and 16 KB).

### Buffer Allocation Metrics (Size = 64)

| Method | Mean | Error | StdDev | P95 | Ratio | Allocated | Alloc Ratio |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **RawAllocation** | **12.621 ns** | 0.8303 ns | 0.9562 ns | 13.511 ns | 1.01 | 88 B | 1.00 |
| **ArrayPool_Shared** | **6.651 ns** | 0.0902 ns | 0.1039 ns | 6.830 ns | 0.53 | 0 B | 0.00 |
| **BufferPoolManager_RentReturn** | **24.164 ns** | 0.0915 ns | 0.0979 ns | 24.316 ns | 1.93 | 0 B | 0.00 |
| **BufferLease_RentDispose** | **41.924 ns** | 0.3952 ns | 0.4551 ns | 42.584 ns | 3.34 | 0 B | 0.00 |

### Buffer Allocation Metrics (Size = 1024)

| Method | Mean | Error | StdDev | P95 | Ratio | Allocated | Alloc Ratio |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **RawAllocation** | **106.740 ns** | 4.1566 ns | 4.7868 ns | 111.626 ns | 1.00 | 1048 B | 1.00 |
| **ArrayPool_Shared** | **6.674 ns** | 0.1748 ns | 0.2013 ns | 6.949 ns | 0.06 | 0 B | 0.00 |
| **BufferPoolManager_RentReturn** | **24.385 ns** | 0.3877 ns | 0.4464 ns | 25.064 ns | 0.23 | 0 B | 0.00 |
| **BufferLease_RentDispose** | **43.809 ns** | 0.8965 ns | 1.0324 ns | 45.152 ns | 0.41 | 0 B | 0.00 |

### Buffer Allocation Metrics (Size = 16384)

| Method | Mean | Error | StdDev | P95 | Ratio | Allocated | Alloc Ratio |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **RawAllocation** | **1,502.927 ns** | 98.8369 ns | 113.8207 ns | 1,596.779 ns | 1.006 | 16408 B | 1.00 |
| **ArrayPool_Shared** | **6.626 ns** | 0.1032 ns | 0.1147 ns | 6.859 ns | 0.004 | 0 B | 0.00 |
| **BufferPoolManager_RentReturn** | **24.543 ns** | 0.2909 ns | 0.2987 ns | 25.069 ns | 0.016 | 0 B | 0.00 |
| **BufferLease_RentDispose** | **106.207 ns** | 0.4275 ns | 0.4752 ns | 106.834 ns | 0.071 | 0 B | 0.00 |

### Why Nalix Memory?

- **Tiered Buffer Rental**: The `BufferPoolManager` optimizes throughput using a multi-path strategy:
    - **Fast Path**: Common block sizes (256B to 4KB) bypass expensive lookup logic via direct array index resolutions.
    - **Adaptive Allocation**: Large allocations fall back to standard `ArrayPool<byte>.Shared` to prevent memory spikes, but return memory cleanly to avoid fragmentation.
- **Lease Safety**: `BufferLease` provides a scope-bound, auto-disposed rental wrapper that keeps rent/return usage allocation-free in the latest benchmark while reducing leak risk in high-complexity code.
- **Trimming & Stability**: The system implements automated shrinking policies to safely return unused memory blocks to the operating system during long periods of low load, keeping the application footprint minimal.

---

## Object Pooling

Memory metrics for reusing class instances via object pools.

| Method | Mean | Error | StdDev | P95 | Ratio | Allocated | Alloc Ratio |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **RawAllocation** | **5.817 ns** | 0.1946 ns | 0.2083 ns | 6.012 ns | 1.00 | 32 B | 1.00 |
| **RentAndReturn_ObjectPool** | **22.817 ns** | 0.3043 ns | 0.3504 ns | 23.398 ns | 3.93 | 0 B | 0.00 |

### Behind the design

- **Object Reusability**: Reusing instances of complex structures (like session states, packet envelopes, and processing contexts) prevents Gen 0 GC thrashing.
- **Hybrid Fast-Path Architecture**:
    - **Thread-Local Cache**: First-level lock-free lookup caches object instances in thread-local storage (`ThreadLocalCache<T>`) for ultra-low latency reuse on the same thread.
    - **Type-Indexed Buckets**: When thread-local slots are empty or full, retrieval falls back to an array-backed lookup using compiled type IDs (`PoolType<T>.Id`) to bypass slow hashing or generic dictionary lookups.
- **Reset Logic**: To prevent state pollution across rentals, pooled objects implement a reset interface (`IPoolable`) that automatically wipes data and prepares the instance for reuse upon its return to the pool.
