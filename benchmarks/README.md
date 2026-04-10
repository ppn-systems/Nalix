# Benchmark Results Summary - 2026-04-10

This repository contains results from **67 performance test suites** demonstrating the efficiency of the Nalix framework across all layers. These results have been integrated into the official documentation at `docs/benchmarks.md`.

## Key Performance Vectors

### 1. High-Frequency Runtime

- **Dependency Injection**: Sub-5ns resolution for cached services (`InstanceManager`).
- **Timing**: ~12ns for monotonic clock ticks and ~87ns for precision network time synchronization.
- **Config**: 4ns lookup times with negligible overhead for hot-reloading.

### 2. Zero-Allocation Memory

- **Buffers**: ~52ns rental overhead with zero GC pressure.
- **Pooling**: Object and List pools achieve ~25-35ns throughput cycles.
- **Data Writers**: Direct binary writing in ~2ns.

### 3. Accelerated Data Path

- **Compression**: Sub-microsecond LZ4 encoding for standard payloads.
- **Framing**: ~250ns for frame transformations and ~3ns for fragmentation checks.
- **Serialization**: ~12ns for structs and arrays; sub-100ns for complex objects.

### 4. Hardened Security

- **Symmetric Encryption**: ~270ns for 64B payloads.
- **AEAD**: ~1.1μs for authenticated encryption (ChaCha20-Poly1305).
- **Primitives**: ~45ns for CSPRNG random generation.

### 5. Scalable Identifiers

- **Snowflake**: ~0.02ns for raw ID construction; ~2.8μs for thread-safe generation.

## Environment Summary

Benchmarks were executed on an **Intel Core i7-13620H** running **.NET 10.0.5** on **Windows 11**.

For full details, including all 67 test suites and payload-specific metrics, see the [Performance Benchmarks](file:///e:/Cs/Nalix/docs/benchmarks.md) guide.
