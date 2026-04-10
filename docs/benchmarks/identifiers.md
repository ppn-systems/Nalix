# Distributed Identifier Benchmarks

Detailed performance metrics for Nalix identifier generation systems, featuring the custom 56-bit Snowflake implementation.

## Snowflake IDs
Ultra-high-throughput, collision-resistant ID generation designed for massive-scale distributed systems.

| Operation | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- |
| **NewId (Components)** | **0.02 ns** | 0.01 ns | 0 B |
| **TryWriteBytes** | **0.42 ns** | 0.01 ns | 0 B |
| **FromBytes** | **0.77 ns** | 0.02 ns | 0 B |
| **ToString (Hex)** | **8.91 ns** | 0.24 ns | 56 B |
| **NewId (Generator)** | **2.87 μs** | 0.16 μs | 0 B |

### Why Nalix Snowflake?
The Nalix Snowflake implementation deviates from the standard 64-bit specification to provide better categorical isolation and extreme memory efficiency using the custom **UInt56** primitive.

- **56-bit Compact Storage**: Stored in exactly 7 bytes via the `UInt56` struct, reducing memory footprint by 12.5% compared to standard `long` identifiers—critical for systems handling billions of IDs in memory.
- **Categorical Isolation**: Includes a dedicated **8-bit Type** field (High bits 48-55), allowing the system to identify the entity category (e.g., Account, Message, Transaction) directly from the ID without a database lookup.
- **Multi-Dimensional Uniqueness**:
    - **Type (8 bits)**: Categorizes the identifier.
    - **MachineId (16 bits)**: Middle bits (32-47) ensure global uniqueness across up to 65,535 nodes.
    - **Computed Value (32 bits)**: Composed of a 20-bit relative millisecond timestamp and a 12-bit sequence (supporting 4,096 IDs per millisecond per category per machine).
- **Concurrency**: Generation is lock-free, utilizing `Interlocked` operations and atomic CAS (Compare-And-Swap) loops to achieve millions of unique IDs per second with zero heap allocations.
