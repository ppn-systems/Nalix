```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method                | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | PayloadBytes | Mean        | Error     | StdDev    | P100        | Allocated |
|---------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |------------- |------------:|----------:|----------:|------------:|----------:|
| Poly1305_Verify       | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           |    157.5 ns |   0.45 ns |   0.27 ns |    157.9 ns |         - |
| Poly1305_Verify       | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           |    161.0 ns |   3.14 ns |   2.94 ns |    166.1 ns |         - |
| Poly1305_Compute      | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           |    226.6 ns |   1.64 ns |   1.08 ns |    228.4 ns |         - |
| Poly1305_Compute      | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           |    233.3 ns |   4.65 ns |   5.17 ns |    238.4 ns |         - |
| Keccak256_HashToSpan  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           |    430.8 ns |   1.53 ns |   0.91 ns |    431.8 ns |         - |
| Keccak256_TryHashData | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           |    436.1 ns |   2.08 ns |   1.24 ns |    437.9 ns |         - |
| Keccak256_TryHashData | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           |    440.8 ns |   7.60 ns |   7.10 ns |    452.1 ns |         - |
| Keccak256_HashToSpan  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           |    454.3 ns |   5.49 ns |   5.13 ns |    459.9 ns |         - |
| Poly1305_Verify       | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096         |  7,366.1 ns |  48.52 ns |  32.09 ns |  7,404.5 ns |         - |
| Poly1305_Verify       | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096         |  7,975.8 ns | 152.45 ns | 149.73 ns |  8,150.1 ns |         - |
| Poly1305_Compute      | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096         | 12,151.2 ns | 141.58 ns |  93.65 ns | 12,288.8 ns |         - |
| Poly1305_Compute      | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096         | 12,407.6 ns | 219.75 ns | 205.55 ns | 12,735.4 ns |         - |
| Keccak256_TryHashData | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096         | 13,321.3 ns |  41.91 ns |  24.94 ns | 13,353.3 ns |         - |
| Keccak256_HashToSpan  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096         | 13,404.9 ns |  62.96 ns |  37.46 ns | 13,486.7 ns |         - |
| Keccak256_HashToSpan  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096         | 13,575.3 ns | 251.62 ns | 235.36 ns | 13,984.9 ns |         - |
| Keccak256_TryHashData | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096         | 13,830.5 ns | 271.58 ns | 301.86 ns | 14,270.4 ns |         - |
