# Serialization Method Comparison

Detailed method-level comparison for `SerializerComparisonBenchmarks`, including all benchmarked methods and simplified API signatures.

## Benchmark Sources

- `docs/Nalix.Benchmark.Framework.Serialization.SerializerComparisonBenchmarks-report-github.md`
- `benchmarks/Nalix.Benchmark.Framework/Serialization/SerializerComparisonBenchmarks.cs`

## Compared Methods

| Library | Benchmark Method | Simplified API Signature |
| :--- | :--- | :--- |
| LiteSerializer | `LiteSerializerSerialize()` | `LiteSerializer.Serialize(payload)` |
| LiteSerializer | `LiteSerializerDeserialize()` | `LiteSerializer.Deserialize<BenchPayload>(bytes)` |
| System.Text.Json | `SystemTextJsonSerialize()` | `JsonSerializer.SerializeToUtf8Bytes(payload)` |
| System.Text.Json | `SystemTextJsonDeserialize()` | `JsonSerializer.Deserialize<BenchPayload>(bytes)` |
| MessagePack | `MessagePackSerialize()` | `MessagePackSerializer.Serialize(payload)` |
| MessagePack | `MessagePackDeserialize()` | `MessagePackSerializer.Deserialize<BenchPayload>(bytes)` |

### Signature Simplification Notes

- The benchmark implementation passes serializer options for `System.Text.Json` and `MessagePack`.
- `LiteSerializer.Deserialize` also includes an `out` consumed-byte parameter in the full call.
- This page intentionally omits those extra parameters to keep cross-library method comparison easier to scan.

---

## Performance Snapshot

| Item Count | Fastest Serialize | Fastest Deserialize | Notes |
| :--- | :--- | :--- | :--- |
| 16 | LiteSerializer (148.4 ns) | LiteSerializer (165.0 ns) | MessagePack serialize is close at 175.7 ns. |
| 128 | MessagePack (262.0 ns) | LiteSerializer (273.1 ns) | LiteSerializer serialize remains close at 298.2 ns. |
| 1024 | LiteSerializer (1,218.9 ns) | LiteSerializer (1,048.9 ns) | LiteSerializer widens the gap at larger payloads. |

## Detailed Results

| Method | Item Count | Mean | Allocated |
| :--- | ---: | ---: | ---: |
| LiteSerializer Serialize | 16 | 148.4 ns | 152 B |
| LiteSerializer Deserialize | 16 | 165.0 ns | 392 B |
| MessagePack Serialize | 16 | 175.7 ns | 128 B |
| MessagePack Deserialize | 16 | 358.0 ns | 392 B |
| System.Text.Json Serialize | 16 | 480.2 ns | 496 B |
| System.Text.Json Deserialize | 16 | 1,029.2 ns | 1168 B |
| MessagePack Serialize | 128 | 262.0 ns | 240 B |
| LiteSerializer Deserialize | 128 | 273.1 ns | 840 B |
| LiteSerializer Serialize | 128 | 298.2 ns | 600 B |
| MessagePack Deserialize | 128 | 851.4 ns | 840 B |
| System.Text.Json Serialize | 128 | 1,266.8 ns | 856 B |
| System.Text.Json Deserialize | 128 | 3,695.8 ns | 2584 B |
| LiteSerializer Deserialize | 1024 | 1,048.9 ns | 4424 B |
| LiteSerializer Serialize | 1024 | 1,218.9 ns | 4184 B |
| MessagePack Serialize | 1024 | 3,374.9 ns | 2800 B |
| MessagePack Deserialize | 1024 | 5,246.8 ns | ~4424 B* |
| System.Text.Json Serialize | 1024 | 7,598.0 ns | 4464 B |
| System.Text.Json Deserialize | 1024 | 21,782.2 ns | 13408 B |

## Key Takeaways

- `LiteSerializer` is the most consistent performer across the full method set, especially on deserialization.
- `MessagePack` can lead in medium-size serialization scenarios.
- `System.Text.Json` shows higher latency and allocation for this benchmark workload.

!!! note
    Allocation for `MessagePack Deserialize` at `ItemCount = 1024` appears inconsistent in the original BenchmarkDotNet report.  
    A direct allocation probe using `GC.GetAllocatedBytesForCurrentThread()` measured approximately **4424 B/op** consistently.
