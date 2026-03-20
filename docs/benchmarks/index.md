# Performance Benchmarks

Nalix is engineered for high-throughput, low-latency real-time applications. This documentation provides a comprehensive report of **67 performance test suites** executed on 2026-04-10.

## Performance Philosophy

The framework achieves exceptional performance through several core design principles:

- **Lock-Free Abstractions**: Minimizing contention using interlocked and thread-local patterns.
- **Zero-Allocation Pipelines**: Reusing memory via advanced pooling for data transfers.
- **Pre-computed Metadata**: Avoiding reflection and string lookups in the hot path.
- **Hardware-Aware Optimizations**: Leveraging SIMD, aggressive inlining, and memory-safe `Span<T>` layouts.

## Benchmark Categories

Explore detailed metrics by subsystem:

- [**Core Infrastructure**](infrastructure.md): DI, Task Scheduling, and Timing.
- [**Memory & Storage**](memory.md): Buffer Pooling and Data Writers.
- [**Data Processing**](data-processing.md): LZ4 and Framing.
- [**Security & Cryptography**](security.md): Engines, Ciphers, and Hashing.
- [**Serialization**](serialization.md): Latest serializer comparison (LiteSerializer, MessagePack, System.Text.Json).
- [**Serialization Methods Compare**](serialization-method-compare.md): Full compared methods with simplified signatures.
- [**Distributed Identifiers**](identifiers.md): Snowflake IDs.

---

## 🚀 Industrial-Grade Benchmarking

Beyond micro-benchmarks, Nalix includes a high-concurrency stress testing tool located in `tools/Nalix.Bench`. This tool is used to validate system stability under localized "worst-case" scenarios.

### Features:
- **High Precision**: Measures Round-Trip Time (RTT) with nanosecond precision.
- **Statistical Tail Latency**: Reports P50, P90, P99, and **P99.99** to identify jitter.
- **Concurrent Load**: Simulations with thousands of sessions and millions of operations.
- **Zero-Allocation Validation**: Monitoring Gen 0/1/2 GC counts during the test.

Usage:
```bash
# Ping-Pong Latency Test (1000 sessions, 100k pings)
./tools/benchmarks.cmd --mode ping --sessions 1000 --count 100000

# Throughput Test (1000 sessions, large payload)
./tools/benchmarks.cmd --mode throughput --sessions 1000 --payload 1024
```

---

## Environment Details

Benchmarks were executed in the following environment:

- **CPU**: 13th Gen Intel Core i7-13620H (2.40GHz)
- **Cores**: 10 Physical, 16 Logical
- **Runtime**: .NET 10.0.5 (X64 RyuJIT)
- **Environment**: Performance Power Plan, Server GC Enabled
- **Toolchain**: BenchmarkDotNet v0.15.8
