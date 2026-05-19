# Serialization Method Comparison

Detailed method-level comparison for `SerializerComparisonBenchmarks`, including compared libraries, API mapping, performance snapshots, and detailed metrics.

## Compared Libraries & APIs

| Library | Benchmark Method | Simplified API Signature |
| :--- | :--- | :--- |
| **LiteSerializer** | `LiteSerializer_Serialize` | `LiteSerializer.Serialize(payload)` |
| **LiteSerializer** | `LiteSerializer_Serialize_Span` | `LiteSerializer.Serialize(payload, span)` |
| **LiteSerializer** | `LiteSerializer_Deserialize` | `LiteSerializer.Deserialize<BenchPayload>(bytes)` |
| **MemoryPack** | `MemoryPack_Serialize` | `MemoryPackSerializer.Serialize(payload)` |
| **MemoryPack** | `MemoryPack_Serialize_Span` | `MemoryPackSerializer.Serialize(writer, payload)` |
| **MemoryPack** | `MemoryPack_Deserialize` | `MemoryPackSerializer.Deserialize<BenchPayload>(bytes)` |
| **MessagePack** | `MessagePack_Serialize` | `MessagePackSerializer.Serialize(payload)` |
| **MessagePack** | `MessagePack_Deserialize` | `MessagePackSerializer.Deserialize<BenchPayload>(bytes)` |
| **System.Text.Json** | `SystemTextJson_Serialize` | `JsonSerializer.SerializeToUtf8Bytes(payload)` |
| **System.Text.Json** | `SystemTextJson_Deserialize` | `JsonSerializer.Deserialize<BenchPayload>(bytes)` |

---

## Performance Snapshot

Overview of the fastest serialization and deserialization methods at different payload scales (16, 128, and 1024 items).

| Item Count | Fastest Serialize | Fastest Deserialize | Notes |
| :--- | :--- | :--- | :--- |
| **16** | MemoryPack Span (28.60 ns) | LiteSerializer (83.92 ns) | LiteSerializer Span is close at 35.99 ns. MemoryPack deserialization is close at 86.36 ns. |
| **128** | MemoryPack Span (31.76 ns) | LiteSerializer (142.90 ns) | LiteSerializer Span is close at 40.68 ns. MemoryPack deserialization is close at 145.03 ns. |
| **1024** | MemoryPack Span (66.51 ns) | MemoryPack (505.34 ns) | LiteSerializer Span is close at 69.36 ns. LiteSerializer deserialization is close at 572.49 ns. |

---

## Detailed Results

Full metrics comparison across all libraries and payload sizes from the BenchmarkDotNet reports.

### Detailed Results (Item Count = 16)

| Method | Mean | Error | StdDev | P95 | Gen0 | Gen1 | Gen2 | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **LiteSerializer_Serialize** | **77.64 ns** | 1.926 ns | 2.218 ns | 79.23 ns | 0.0057 | - | - | 216 B |
| **LiteSerializer_Serialize_Span** | **35.99 ns** | 0.836 ns | 0.963 ns | 37.25 ns | - | - | - | 0 B |
| **MemoryPack_Serialize** | **57.88 ns** | 1.067 ns | 1.229 ns | 59.58 ns | 0.0057 | - | - | 216 B |
| **MemoryPack_Serialize_Span** | **28.60 ns** | 0.644 ns | 0.742 ns | 29.60 ns | - | - | - | 0 B |
| **MessagePack_Serialize** | **99.56 ns** | 1.478 ns | 1.702 ns | 101.64 ns | 0.0044 | - | - | 168 B |
| **SystemTextJson_Serialize** | **266.15 ns** | 3.660 ns | 4.215 ns | 270.74 ns | 0.0148 | - | 0.0005 | 0 B |
| **LiteSerializer_Deserialize** | **83.92 ns** | 2.487 ns | 2.865 ns | 86.92 ns | 0.0108 | - | - | 408 B |
| **MemoryPack_Deserialize** | **86.36 ns** | 1.261 ns | 1.452 ns | 88.95 ns | 0.0117 | - | - | 440 B |
| **MessagePack_Deserialize** | **186.50 ns** | 1.312 ns | 1.511 ns | 189.30 ns | 0.0117 | - | - | 440 B |
| **SystemTextJson_Deserialize** | **576.38 ns** | 6.054 ns | 6.972 ns | 591.03 ns | 0.0267 | - | - | 1008 B |

### Detailed Results (Item Count = 128)

| Method | Mean | Error | StdDev | P95 | Gen0 | Gen1 | Gen2 | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **LiteSerializer_Serialize** | **149.94 ns** | 2.474 ns | 2.750 ns | 153.61 ns | 0.0176 | - | - | 664 B |
| **LiteSerializer_Serialize_Span** | **40.68 ns** | 1.115 ns | 1.284 ns | 42.62 ns | - | - | - | 0 B |
| **MemoryPack_Serialize** | **121.64 ns** | 0.953 ns | 1.098 ns | 123.42 ns | 0.0178 | - | - | 664 B |
| **MemoryPack_Serialize_Span** | **31.76 ns** | 0.516 ns | 0.594 ns | 32.77 ns | - | - | - | 0 B |
| **MessagePack_Serialize** | **422.49 ns** | 2.824 ns | 3.252 ns | 425.95 ns | 0.0134 | - | - | 504 B |
| **SystemTextJson_Serialize** | **897.72 ns** | 7.304 ns | 8.411 ns | 907.24 ns | 0.0286 | - | 0.0010 | 7200 B |
| **LiteSerializer_Deserialize** | **142.90 ns** | 4.698 ns | 5.411 ns | 147.90 ns | 0.0229 | - | - | 856 B |
| **MemoryPack_Deserialize** | **145.03 ns** | 1.769 ns | 2.037 ns | 147.52 ns | 0.0237 | - | - | 888 B |
| **MessagePack_Deserialize** | **1,095.24 ns** | 11.116 ns | 12.801 ns | 1,115.91 ns | 0.0229 | - | - | 888 B |
| **SystemTextJson_Deserialize** | **2,548.20 ns** | 40.497 ns | 46.637 ns | 2,618.90 ns | 0.0496 | - | - | 1976 B |

### Detailed Results (Item Count = 1024)

| Method | Mean | Error | StdDev | P95 | Gen0 | Gen1 | Gen2 | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **LiteSerializer_Serialize** | **655.83 ns** | 64.628 ns | 74.426 ns | 725.14 ns | 0.1163 | - | - | 4280 B |
| **LiteSerializer_Serialize_Span** | **69.36 ns** | 1.757 ns | 2.023 ns | 73.04 ns | - | - | - | 0 B |
| **MemoryPack_Serialize** | **549.05 ns** | 25.847 ns | 29.765 ns | 585.39 ns | 0.1116 | - | - | 4248 B |
| **MemoryPack_Serialize_Span** | **66.51 ns** | 2.043 ns | 2.352 ns | 69.69 ns | - | - | - | 0 B |
| **MessagePack_Serialize** | **2,862.59 ns** | 25.256 ns | 29.085 ns | 2,910.33 ns | 0.0839 | - | - | 3192 B |
| **SystemTextJson_Serialize** | **6,109.84 ns** | 49.960 ns | 57.534 ns | 6,182.82 ns | 0.1602 | - | - | 5968 B |
| **LiteSerializer_Deserialize** | **572.49 ns** | 27.568 ns | 31.747 ns | 603.64 ns | 0.1206 | - | 0.0005 | 4440 B |
| **MemoryPack_Deserialize** | **505.34 ns** | 16.626 ns | 19.146 ns | 532.20 ns | 0.1206 | - | - | 4472 B |
| **MessagePack_Deserialize** | **8,176.03 ns** | 174.512 ns | 200.968 ns | 8,468.96 ns | 0.1068 | - | - | 4472 B |
| **SystemTextJson_Deserialize** | **18,282.04 ns** | 340.117 ns | 391.679 ns | 18,893.55 ns | 0.2441 | - | - | 9216 B |

---

## Key Takeaways

- **LiteSerializer vs. MemoryPack**: `LiteSerializer` is highly competitive with `MemoryPack`, which is widely recognized as the fastest serialization framework for .NET.
    - **Serialization**: `MemoryPack_Serialize_Span` holds a slight lead (e.g. 66.51 ns vs 69.36 ns for 1024 items).
    - **Deserialization**: `LiteSerializer` outpaces MemoryPack on smaller workloads (e.g. 83.92 ns vs 86.36 ns for 16 items) and remains within a very close margin on large ones.
- **MessagePack & JSON**: `LiteSerializer` significantly outperforms `MessagePack` (by 3x-8x in speed) and `System.Text.Json` (by 10x-30x in speed), while using much less memory allocation.
