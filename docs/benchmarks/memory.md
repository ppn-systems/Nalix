# Memory & Storage Benchmarks

Nalix uses a highly optimized memory management subsystem designed to eliminate Garbage Collection (GC) pauses in high-throughput workloads.

## Buffer Pooling & Leases

Comparison of memory acquisition strategies across various sizes (64 B, 1 KB, and 16 KB).

### Buffer Allocation Metrics (Size = 64)

| Method | Mean | Error | StdDev | P95 | Ratio | Allocated | Alloc Ratio |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **RawAllocation** | **19.07 ns** | 0.544 ns | 0.582 ns | 19.81 ns | 1.00 | 88 B | 1.00 |
| **ArrayPool_Shared** | **10.50 ns** | 0.267 ns | 0.308 ns | 10.94 ns | 0.55 | 0 B | 0.00 |
| **BufferPoolManager_RentReturn** | **48.89 ns** | 1.907 ns | 2.196 ns | 51.30 ns | 2.57 | 0 B | 0.00 |
| **BufferLease_RentDispose** | **113.93 ns** | 5.300 ns | 5.891 ns | 124.09 ns | 5.98 | 32 B | 0.36 |

### Buffer Allocation Metrics (Size = 1024)

| Method | Mean | Error | StdDev | P95 | Ratio | Allocated | Alloc Ratio |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **RawAllocation** | **202.23 ns** | 5.746 ns | 6.387 ns | 212.76 ns | 1.00 | 1048 B | 1.00 |
| **ArrayPool_Shared** | **10.48 ns** | 0.279 ns | 0.321 ns | 10.83 ns | 0.05 | 0 B | 0.00 |
| **BufferPoolManager_RentReturn** | **49.99 ns** | 0.989 ns | 1.099 ns | 50.83 ns | 0.25 | 0 B | 0.00 |
| **BufferLease_RentDispose** | **114.38 ns** | 3.450 ns | 3.972 ns | 119.47 ns | 0.57 | 32 B | 0.03 |

### Buffer Allocation Metrics (Size = 16384)

| Method | Mean | Error | StdDev | P95 | Ratio | Allocated | Alloc Ratio |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **RawAllocation** | **2,856.89 ns** | 32.918 ns | 36.589 ns | 2,913.87 ns | 1.000 | 16408 B | 1.000 |
| **ArrayPool_Shared** | **10.87 ns** | 0.121 ns | 0.139 ns | 11.03 ns | 0.004 | 0 B | 0.000 |
| **BufferPoolManager_RentReturn** | **56.79 ns** | 1.219 ns | 1.305 ns | 58.22 ns | 0.020 | 0 B | 0.000 |
| **BufferLease_RentDispose** | **336.11 ns** | 20.650 ns | 23.780 ns | 368.53 ns | 0.118 | 32 B | 0.002 |

### Why Nalix Memory?

- **Tiered Buffer Rental**: The `BufferPoolManager` optimizes throughput using a multi-path strategy:
    - **Fast Path**: Common block sizes (256B to 4KB) bypass expensive lookup logic via direct array index resolutions.
    - **Adaptive Allocation**: Large allocations fall back to standard `ArrayPool<byte>.Shared` to prevent memory spikes, but return memory cleanly to avoid fragmentation.
- **Lease Safety**: `BufferLease` provides a scope-bound, auto-disposed rental wrapper. Despite a small allocation overhead of 32 bytes for the lease reference wrapper, it ensures memory leaks are completely prevented in high-complexity code.
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
