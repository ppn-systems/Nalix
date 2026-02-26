# Data Processing & Framing Benchmarks

Detailed performance metrics for Nalix data processing pipelines, including compression and framing logic.

## LZ4 Compression
Native integration with buffer leases for zero-allocation compression and decompression.

| Payload Size | Operation | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- | :--- |
| **1 KB** | Encode (to Span) | **118.2 ns** | 0.42 ns | 0 B |
| **1 KB** | Decode (to Span) | **296.9 ns** | 6.25 ns | 0 B |
| **16 KB** | Encode (to Span) | **1.19 μs** | 5.52 ns | 0 B |
| **16 KB** | Decode (to Span) | **4.66 μs** | 59.0 ns | 0 B |

### Technical Excellence
- **Zero-Allocation**: Unlike standard LZ4 wrappers that allocate `byte[]` arrays, Nalix operates directly on rentals from the `BufferPool`, achieving sub-microsecond latency without triggering the GC.
- **Adaptive Dictionary**: The compressor automatically adjusts its internal dictionary size based on the input payload, ensuring optimal performance for both micro-packets (256B) and larger frames (16KB+).

---

## Framing & Transformation
Core pipeline transformations for high-frequency real-time traffic.

| Operation | Scenario | Latency (Mean) | StdDev | Allocation |
| :--- | :--- | :--- | :--- | :--- |
| **Transformation Check** | 256B Payload | **250.1 ns** | 0.64 ns | 0 B |
| **Encryption Transform** | 256B Payload | **1.48 μs** | 0.76 ns | 0 B |
| **Fragmentation Check** | 16 Chunks | **2.57 ns** | 0.04 ns | 0 B |
| **Sequential Assembly** | 4 Chunks | **107.27 ns** | 2.60 ns | 368 B |

### Why Nalix Data Processing?
High-throughput data transformations in Nalix are optimized for CPU cache efficiency and zero heap pressure.

- **Zero-Copy LZ4 Pipeline**: The `LZ4Codec` provides a span-based API that performs compression and decompression directly within pooled `BufferLease` memory. This eliminates intermediate allocations and ensures that data is only copied when necessary for transmission.
- **Low-Latency Framing**: Large data payloads are managed using a branchless framing engine that utilizes bit-shifting and CPU intrinsics for header processing and CRC32 verification.
- **Atomic Assembly**: The `FragmentAssembler` utilizes a slot-indexed assembly strategy to reorder and merge incoming packet fragments with minimal memory movement, ensuring that complete messages are delivered to the application layer at lightning speed.
