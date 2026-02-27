# Core Infrastructure Benchmarks

Detailed performance metrics for the Nalix core runtime, including dependency injection, task management, and timing systems.

## Dependency Injection (InstanceManager)
The `InstanceManager` provides lighting-fast service resolution using a multi-level caching system. 

| Scenario | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- |
| **L1 Cache Hit** (Thread-Local) | **4.35 ns** | 0.01 ns | 0 B |
| **Generic Slot Hit** | **4.39 ns** | 0.01 ns | 0 B |
| **Dictionary Fallback** | **4.40 ns** | 0.04 ns | 0 B |
| **Type-based Lookup** | **116.73 ns** | 2.04 ns | 64 B |

### Why Nalix Infrastructure?
Nalix core services are engineered for sub-nanosecond resolution and massive concurrency.

- **Multi-Level DI Caching**: The `InstanceManager` utilizes a three-tier resolution strategy:
    - **Tier 1 (Generic Slot)**: Static class fields for direct type-to-instance access, providing sub-1ns resolution.
    - **Tier 2 (Thread L1)**: `[ThreadStatic]` storage to eliminate dictionary lookups for frequently accessed per-thread services.
    - **Tier 3 (Concurrent Global)**: A high-performance lock-free dictionary fallback for global singleton resolution.
- **Intelligent Task Scheduling**: The `TaskManager` manages thousands of concurrent workers using:
    - **Worker Grouping**: Isolation and concurrency limits per group via `Gate` abstractions.
    - **Dynamic Adjustment**: Continuous CPU monitoring to auto-throttle worker density during high system pressure.
- **Monotonic Time Synchronization**: The `Clock` system provides a high-resolution UTC estimate anchored to the monotonic `Stopwatch.GetTimestamp()`, featuring drift smoothing to maintain precision without system clock jitter.

---

## Task System
Optimized task workers for high-frequency recurring jobs.

| Operation | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- |
| **RunOnceAsync (No-Op)** | **2.48 μs** | 0.05 μs | 4.3 KB |
| **Schedule Worker & Wait** | **9.77 ms** | 2.62 ms | 7.7 KB |
| **Generate Tracking Report** | **2.40 ms** | 0.14 ms | 31.3 KB |

### Behind the design
- **Worker Affinity**: Tasks are pinned to specific worker threads to maximize L1/L2 cache locality.
- **Non-Blocking Reports**: Status reports are generated using atomic snapshots, ensuring that monitoring doesn't impact task execution latency.

---

## Clock & Timing
High-precision monotonic clock and network time synchronization.

| Operation | Latency (Mean) | StdDev |
| :--- | :--- | :--- |
| **Monotonic Ticks (Now)** | **11.89 ns** | 0.02 ns |
| **UTC Now** | **16.12 ns** | 0.02 ns |
| **Unix/Epoch Milliseconds** | **23.48 ns** | 0.04 ns |
| **RTT Sync (Precision)** | **87.50 ns** | 106.06 ns |

### Precision Engineering
- **Monotonic Source**: Uses `Stopwatch.GetTimestamp()` as the primary tick source to avoid system clock drifts and "time jumps."
- **Low Overhead**: Ticks are updated via high-speed hardware timers, allowing thousands of nanosecond-accurate measurements per second.

---

## Configuration Manager
Minimal overhead for configuration access and hot-reloading.

| Scenario | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- |
| **IsLoaded Check (Hit)** | **4.06 ns** | 0.01 ns | 0 B |
| **Get Access (Cache Hit)** | **18.15 ns** | 0.11 ns | 32 B |
| **First Load (File I/O)** | **2.74 μs** | 0.31 μs | 472 B |
| **Hot Reload (2 Containers)** | **31.09 μs** | 0.09 μs | 13.4 KB |

### How it works
- **Atomic Swap**: Hot-reloading uses atomic reference swapping, meaning threads always see a consistent state without ever being blocked by a reload operation.
- **Cache-Friendly Layout**: Configuration entries are stored in contiguous memory layouts to minimize pointer chasing.
