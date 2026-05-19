# Serialization Benchmarks

Nalix features a custom binary serialization engine, `LiteSerializer`, designed for maximum throughput and minimal allocation in high-performance network transport.

## LiteSerializer Performance

Detailed micro-benchmarks of standard serialization operations, including unmanaged structs, custom formatters, inline span fills, and formatter resolving.

| Method | Mean | Error | StdDev | P95 | Gen0 | Gen1 | Gen2 | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **Serialize_Unmanaged_Small** | **8.2995 ns** | 0.2650 ns | 0.2836 ns | 8.769 ns | 0.0019 | - | - | 56 B |
| **Serialize_Unmanaged_Large** | **65.4580 ns** | 5.3494 ns | 6.1603 ns | 69.098 ns | 0.0144 | - | - | 536 B |
| **Deserialize_Unmanaged_Small** | **4.3659 ns** | 0.1008 ns | 0.1161 ns | 4.524 ns | - | - | - | 0 B |
| **Deserialize_Unmanaged_Large** | **24.7143 ns** | 0.6311 ns | 0.7268 ns | 25.938 ns | - | - | - | 0 B |
| **Serialize_Formatter** | **74.5022 ns** | 0.6677 ns | 0.7689 ns | 75.573 ns | 0.0066 | 0.0002 | 0.0002 | 385 B |
| **Deserialize_Formatter** | **70.5643 ns** | 1.8457 ns | 2.1255 ns | 73.375 ns | 0.0079 | - | - | 296 B |
| **Fill_IntoSpan_Small** | **0.9546 ns** | 0.0449 ns | 0.0517 ns | 1.034 ns | - | - | - | 0 B |
| **Fill_IntoSpan_Large** | **5.4546 ns** | 0.0610 ns | 0.0678 ns | 5.590 ns | - | - | - | 0 B |
| **Resolve_Formatter** | **1.1719 ns** | 0.0320 ns | 0.0342 ns | 1.218 ns | - | - | - | 0 B |

### Why Nalix Serialization?

- **Zero-Allocation Deserialization**: Deserialization of unmanaged structures (`Deserialize_Unmanaged_Small` and `Deserialize_Unmanaged_Large`) is fully allocation-free (0 B) and runs in single-digit to low double-digit nanoseconds.
- **Fast Path Span Fills**: Copying primitive data directly into target spans (`Fill_IntoSpan_Small`) takes under **1 nanosecond** (~0.95 ns) with zero overhead.
- **Direct Memory Blitting**: For unmanaged structs, `LiteSerializer` uses low-level memory block copying via the `Unsafe` class, producing CPU instructions that operate at raw hardware limits.
- **Aggressive Inlining**: Key methods in the serialization path are decorated with aggressive compilation attributes, allowing RyuJIT to inline and optimize out method call overhead.
