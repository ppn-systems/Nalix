
```log
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
| EnvelopeEncryptor.Decrypt | SALSA20           | 22.90 μs | 0.590 μs | 1.720 μs | 22.10 μs | 20.90 μs | 28.00 μs |    1 |     718 B |   2.27 KB |
| EnvelopeEncryptor.Encrypt | SALSA20           | 30.23 μs | 0.607 μs | 1.062 μs | 29.90 μs | 28.90 μs | 33.10 μs |    2 |     515 B |   4.49 KB |
| EnvelopeEncryptor.Decrypt | SALSA20_POLY1305  | 32.05 μs | 0.636 μs | 1.535 μs | 31.90 μs | 25.70 μs | 34.90 μs |    3 |     718 B |   2.27 KB |
| EnvelopeEncryptor.Decrypt | CHACHA20          | 33.68 μs | 1.062 μs | 2.995 μs | 33.50 μs | 28.30 μs | 41.20 μs |    3 |     718 B |    2.7 KB |
| EnvelopeEncryptor.Encrypt | SALSA20_POLY1305  | 38.19 μs | 0.758 μs | 1.710 μs | 37.85 μs | 35.45 μs | 42.05 μs |    4 |     515 B |   5.01 KB |
| EnvelopeEncryptor.Encrypt | CHACHA20          | 38.83 μs | 0.727 μs | 1.533 μs | 38.90 μs | 35.80 μs | 42.10 μs |    4 |     515 B |   4.63 KB |
| EnvelopeEncryptor.Decrypt | CHACHA20_POLY1305 | 43.39 μs | 0.851 μs | 1.247 μs | 43.40 μs | 40.50 μs | 46.10 μs |    5 |     718 B |   2.27 KB |
| EnvelopeEncryptor.Encrypt | CHACHA20_POLY1305 | 49.06 μs | 0.920 μs | 1.751 μs | 49.05 μs | 46.45 μs | 52.75 μs |    6 |     515 B |   5.13 KB |
