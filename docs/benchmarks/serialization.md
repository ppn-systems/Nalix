# Serialization Benchmarks

Nalix features a custom binary serialization engine designed for maximum throughput and minimal allocation.

## Object Serialization

Performance for complex class-based POCOs (Plain Old CLR Objects).

| Operation | Item Count | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- | :--- |
| **Serialize Object** | 16 | **61.11 ns** | 1.22 ns | 112 B |
| **Deserialize Object** | 16 | **30.19 ns** | 0.69 ns | 176 B |
| **Serialize Object** | 128 | **81.00 ns** | 1.74 ns | 560 B |
| **Deserialize Object** | 128 | **47.97 ns** | 1.00 ns | 624 B |

### Why it's efficient

- **Reflection-Free**: The `LiteSerializer` skips reflection and uses source-generated or manual binary mapping, avoiding the heavy overhead typical of JSON or XML serializers.
- **Zero-Copy Pipelines**: Objects are serialized directly into pooled memory buffers, eliminating the need for temporary string or array intermediates.

---

## Struct & Array Serialization

High-speed binary layout processing for primitive types and collection data.

| Operation | Scenario | Latency (Mean) | StdDev |
| :--- | :--- | :--- | :--- |
| **Serialize Struct** | Into Span | **12.09 ns** | 0.14 ns |
| **Serialize Array** | 16 Items | **12.72 ns** | 0.22 ns |
| **Deserialize Array** | 128 Items | **68.40 ns** | 1.02 ns |
| **Serialize Array** | 1024 Items | **156.12 ns** | 3.76 ns |

### Why Nalix Serialization?

The `LiteSerializer` is the backbone of Nalix's data transport, providing a reflection-free path for mapping CLR objects to binary formats.

- **Unmanaged Hot-Paths**: For primitive types and unmanaged structs, the serializer uses `GC.AllocateUninitializedArray` combined with `Unsafe.WriteUnaligned` and `Unsafe.CopyBlockUnaligned`. This bypasses expensive clearing logic and achieves raw memory copy speeds.
- **Zero-Allocation Deserialization**: The `DataReader` provides a non-copying view of binary payloads, allowing the application to resolve objects directly from incoming transmission buffers without intermediate heap pressure.
- **DataWriter Orchestration**: Serialization utilizes a pooled `DataWriter` that orchestrates binary packing using low-level memory operations, ensuring that complex object graphs are serialized at sub-microsecond latencies.
- **JIT & ASM Optimization**: All hot paths are decorated with `AggressiveInlining` and `AggressiveOptimization`, ensuring that the .NET JIT compiler produces optimal assembly with minimal overhead.

!!! note
    Detailed assembly analysis (AMD64) confirms that struct serialization produces optimal instruction sequences with no heap-based tracking overhead.
