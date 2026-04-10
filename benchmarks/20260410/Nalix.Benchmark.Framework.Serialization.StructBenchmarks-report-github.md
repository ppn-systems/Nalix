```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method             | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | Mean     | Error    | StdDev   | P100     | Ratio | RatioSD | Code Size | Gen0   | Allocated | Alloc Ratio |
|------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |---------:|---------:|---------:|---------:|------:|--------:|----------:|-------:|----------:|------------:|
| Serialize_IntoSpan | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 12.09 ns | 0.146 ns | 0.137 ns | 12.25 ns |  0.73 |    0.02 |   2,460 B |      - |         - |        0.00 |
| Serialize          | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16.63 ns | 0.346 ns | 0.324 ns | 16.94 ns |  1.00 |    0.03 |   1,396 B | 0.0063 |      80 B |        1.00 |
|                    |           |                  |                  |        |           |                |             |             |             |          |          |          |          |       |         |           |        |           |             |
| Serialize_IntoSpan | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 13.71 ns | 3.447 ns | 2.280 ns | 16.44 ns |  0.75 |    0.12 |   2,521 B |      - |         - |        0.00 |
| Serialize          | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 18.39 ns | 0.281 ns | 0.186 ns | 18.70 ns |  1.00 |    0.01 |   1,396 B | 0.0063 |      80 B |        1.00 |
