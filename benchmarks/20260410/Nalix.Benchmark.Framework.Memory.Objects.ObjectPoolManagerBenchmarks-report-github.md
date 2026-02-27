```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method               | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | Mean      | Error     | StdDev    | Median    | P100      | Gen0   | Allocated |
|--------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |----------:|----------:|----------:|----------:|----------:|-------:|----------:|
| PerformHealthCheck   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |  36.31 ns |  0.095 ns |  0.050 ns |  36.32 ns |  36.38 ns |      - |         - |
| PerformHealthCheck   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |  36.73 ns |  0.587 ns |  0.549 ns |  36.98 ns |  37.40 ns |      - |         - |
| TypedPool_Get_Return | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |  40.31 ns |  0.365 ns |  0.342 ns |  40.37 ns |  40.84 ns | 0.0025 |      32 B |
| TypedPool_Get_Return | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |  71.01 ns | 56.249 ns | 37.205 ns |  42.54 ns | 115.08 ns | 0.0024 |      32 B |
| Get_Return           | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 119.22 ns |  0.724 ns |  0.677 ns | 119.35 ns | 120.28 ns | 0.0024 |      32 B |
| Get_Return           | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 125.74 ns |  0.492 ns |  0.293 ns | 125.79 ns | 126.25 ns | 0.0024 |      32 B |
