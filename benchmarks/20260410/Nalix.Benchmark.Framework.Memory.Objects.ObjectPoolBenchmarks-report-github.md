```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method               | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | Mean     | Error    | StdDev   | P100     | Gen0   | Allocated |
|--------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |---------:|---------:|---------:|---------:|-------:|----------:|
| Prealloc             | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 13.00 ns | 0.277 ns | 0.259 ns | 13.43 ns | 0.0019 |      24 B |
| Prealloc             | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 20.83 ns | 6.705 ns | 4.435 ns | 23.89 ns | 0.0019 |      24 B |
| TypedPool_Get_Return | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32.45 ns | 0.403 ns | 0.377 ns | 32.97 ns | 0.0025 |      32 B |
| Get_Return           | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32.98 ns | 0.416 ns | 0.275 ns | 33.39 ns | 0.0024 |      32 B |
| TypedPool_Get_Return | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 33.01 ns | 0.288 ns | 0.171 ns | 33.26 ns | 0.0024 |      32 B |
| Get_Return           | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 33.22 ns | 0.632 ns | 0.728 ns | 35.00 ns | 0.0025 |      32 B |
