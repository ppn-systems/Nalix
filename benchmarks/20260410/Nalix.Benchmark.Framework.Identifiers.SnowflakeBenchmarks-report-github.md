```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method               | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | Mean          | Error       | StdDev      | P100          | Gen0   | Allocated |
|--------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |--------------:|------------:|------------:|--------------:|-------:|----------:|
| NewId_FromComponents | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |     0.0283 ns |   0.0098 ns |   0.0058 ns |     0.0415 ns |      - |         - |
| NewId_FromComponents | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |     0.1721 ns |   0.0149 ns |   0.0139 ns |     0.1854 ns |      - |         - |
| TryWriteBytes        | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |     0.4249 ns |   0.0212 ns |   0.0140 ns |     0.4489 ns |      - |         - |
| TryWriteBytes        | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |     0.4540 ns |   0.0269 ns |   0.0251 ns |     0.4967 ns |      - |         - |
| FromBytes            | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |     0.7734 ns |   0.0356 ns |   0.0236 ns |     0.8031 ns |      - |         - |
| FromBytes            | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |     0.8273 ns |   0.0319 ns |   0.0299 ns |     0.8829 ns |      - |         - |
| ToByteArray          | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |     3.6428 ns |   0.1140 ns |   0.1313 ns |     3.8581 ns | 0.0025 |      32 B |
| ToStringHex          | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |     8.9134 ns |   0.2190 ns |   0.2434 ns |     9.3074 ns | 0.0045 |      56 B |
| ToStringHex          | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |     9.6719 ns |   0.7302 ns |   0.4830 ns |    10.3798 ns | 0.0038 |      56 B |
| ToByteArray          | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |    17.4421 ns |  21.4132 ns |  14.1635 ns |    31.4837 ns | 0.0025 |      32 B |
| NewId_FromGenerator  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 2,879.9169 ns |  57.0597 ns | 161.8689 ns | 3,211.9785 ns |      - |         - |
| NewId_FromGenerator  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 3,301.0201 ns | 492.6794 ns | 325.8770 ns | 3,565.6631 ns |      - |         - |
