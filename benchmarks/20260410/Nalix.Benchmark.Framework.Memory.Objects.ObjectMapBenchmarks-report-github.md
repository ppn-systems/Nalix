```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method               | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | ItemCount | Mean       | Error      | StdDev     | P100       | Gen0   | Gen1   | Allocated |
|--------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |---------- |-----------:|-----------:|-----------:|-----------:|-------:|-------:|----------:|
| Rent_Add_Read_Return | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32        |   1.421 μs |  0.0276 μs |  0.0272 μs |   1.461 μs | 0.2422 | 0.0019 |   2.98 KB |
| Rent_Add_Read_Return | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32        |   3.542 μs |  0.4114 μs |  0.2721 μs |   3.937 μs | 0.3204 |      - |   3.93 KB |
| Rent_Add_Read_Return | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256       |  64.061 μs |  0.7379 μs |  0.6542 μs |  64.973 μs | 4.3945 | 0.3662 |  55.06 KB |
| Rent_Add_Read_Return | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256       | 114.125 μs | 44.6934 μs | 29.5619 μs | 133.774 μs | 4.3945 |      - |  55.06 KB |
