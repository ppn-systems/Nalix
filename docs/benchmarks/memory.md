# Memory & Storage Benchmarks

Nalix uses a specialized memory management system designed to eliminate Garbage Collection (GC) pauses in the hot path.

## Buffer Pooling & Leases
Standardized rental system for high-performance I/O operations.

### Basic Pooling
| Operation | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- |
| **Rent & Return** (Segment/Array) | **51 - 53 ns** | 0.15 ns | 0 B |
| **Allocation Size Check** | **0.3 - 3.1 ns** | 0.01 ns | 0 B |

### Buffer Leases
High-performance temporary leases for local data processing.

| Operation | Latency (128B) | Latency (2KB) | StdDev | Allocation |
| :--- | :--- | :--- | :--- | :--- |
| **Rent & Dispose** | **23.39 ns** | **37.47 ns** | 0.15 ns | 48 B |
| **Copy & Dispose** | **23.30 ns** | **41.24 ns** | 0.19 ns | 48 B |

### Why Nalix Memory?
The Nalix memory sub-system is designed to eliminate the costs of the .NET Garbage Collector in high-load scenarios.

- **Tiered Buffer Rental**: The `BufferPoolManager` optimizes throughput using a three-path strategy:
    - **Fast Path**: Common sizes (256B to 4KB) bypass resolution logic via direct pool indexing.
    - **Adaptive Cache**: A `suitablePoolSizeCache` tracks optimal pool matches for irregular request sizes.
    - **Fallback**: Integration with `ArrayPool<byte>.Shared` ensures the system remains operational even during extreme allocation spikes.
- **Buffer Stability (Trimming)**: Implements a **ShrinkSafetyPolicy** that conservatively returns memory to the OS. It requires a 50% idle threshold and uses a multi-cycle step-down approach to prevent pool oscillation.
- **Type-Specific Object Pooling**: The `ObjectPool` utilizes dedicated `TypePool` buckets for every pooled type, ensuring zero-allocation reuse of `IPoolable` objects with mandatory `ResetForPool` calls on return.

---

## Object & Collection Pooling
Reusable pools for lists, maps, and class instances.

### Generic Object Pooling
| Operation | Scenario | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- | :--- |
| **Preallocate Pool** | 10 Items | **13.00 ns** | 0.26 ns | 24 B |
| **Object Pool Get/Return** | Typed | **32.45 ns** | 0.38 ns | 32 B |

### List & Map Collections
| Operation | Scenario | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- | :--- |
| **List Pool Trim** | 256 Items | **24.54 ns** | 0.07 ns | 0 B |
| **List Pool Rent/Fill** | 32 Items | **70.75 ns** | 2.60 ns | 0 B |
| **Object Map (Rent/Add/Read)**| 32 Items | **1.42 μs** | 0.03 μs | 2.98 KB |

### Pool Management (Health Checks)
| Operation | Latency (Mean) | StdDev |
| :--- | :--- | :--- |
| **Health Check (No Cleanup)** | **36.31 ns** | 0.05 ns |
| **Typed Pool Management Cycle** | **40.31 ns** | 0.34 ns |

### Behind the design
- **Pre-warming**: Pools can be pre-warmed during application startup to avoid first-allocation latency spikes.
- **Dynamic Sizing**: The `ObjectPoolManager` monitors usage frequency and automatically trims idle objects to maintain a minimal memory footprint.

---

## Data Writers
Fast, low-allocation builders for binary protocols.

| Strategy | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- |
| **Fixed Array Writer** | **2.38 ns** | 0.09 ns | 0 B |
| **Rented Buffer Writer** | **10.05 ns** | 0.04 ns | 0 B |
| **Expandable Writer** | **21.28 ns** | 0.22 ns | 0 B |

### Optimization Strategy
- **Direct Span Access**: Writers operate directly on `Span<byte>`, leveraging SIMD instructions and avoiding intermediate copying during serialization.
- **Zero-allocation Expansion**: When a buffer needs to grow, the writer rents a new, larger segment from the pool and returns the old one immediately.
