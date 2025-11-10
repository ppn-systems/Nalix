```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]    : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  Toolchain=net10.0  
InvocationCount=1  UnrollFactor=1  

```
| Method                    | Algorithm         | Mean     | Error    | StdDev   | Median   | Min      | Max      | Rank | Code Size | Allocated |
|-------------------------- |------------------ |---------:|---------:|---------:|---------:|---------:|---------:|-----:|----------:|----------:|
| EnvelopeEncryptor.Decrypt | SALSA20           | 25.51 μs | 0.584 μs | 1.600 μs | 25.50 μs | 22.70 μs | 30.30 μs |    1 |     718 B |   2.27 KB |
| EnvelopeEncryptor.Encrypt | SALSA20           | 32.17 μs | 0.788 μs | 2.223 μs | 32.20 μs | 28.30 μs | 38.00 μs |    2 |     515 B |    4.3 KB |
| EnvelopeEncryptor.Decrypt | CHACHA20          | 32.78 μs | 0.758 μs | 2.150 μs | 32.70 μs | 28.40 μs | 38.10 μs |    2 |     718 B |    2.7 KB |
| EnvelopeEncryptor.Decrypt | SALSA20_POLY1305  | 35.11 μs | 0.747 μs | 2.154 μs | 34.85 μs | 30.65 μs | 41.05 μs |    3 |     718 B |   2.27 KB |
| EnvelopeEncryptor.Encrypt | SALSA20_POLY1305  | 38.40 μs | 1.525 μs | 4.399 μs | 36.90 μs | 33.20 μs | 50.50 μs |    3 |     515 B |   4.82 KB |
| EnvelopeEncryptor.Encrypt | CHACHA20          | 38.71 μs | 0.927 μs | 2.614 μs | 38.55 μs | 34.30 μs | 45.10 μs |    3 |     515 B |   4.45 KB |
| EnvelopeEncryptor.Decrypt | CHACHA20_POLY1305 | 46.77 μs | 0.945 μs | 2.712 μs | 46.10 μs | 41.60 μs | 53.70 μs |    4 |     718 B |   2.27 KB |
| EnvelopeEncryptor.Encrypt | CHACHA20_POLY1305 | 49.74 μs | 1.389 μs | 3.895 μs | 49.25 μs | 43.35 μs | 61.05 μs |    4 |     515 B |   4.94 KB |
