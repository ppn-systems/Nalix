```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method                              | Job       | MinIterationTime | Affinity         | Server | Toolchain | InvocationCount | IterationCount | LaunchCount | RunStrategy | UnrollFactor | WarmupCount | Mean      | Error      | StdDev     | Median    | P100        | Allocated |
|------------------------------------ |---------- |----------------- |----------------- |------- |---------- |---------------- |--------------- |------------ |------------ |------------- |------------ |----------:|-----------:|-----------:|----------:|------------:|----------:|
| MonoTicksNow                        | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default         | Default        | Default     | Default     | 16           | Default     |  11.89 ns |   0.028 ns |   0.024 ns |  11.88 ns |    11.94 ns |         - |
| MonoTicksNow                        | Net10     | 250ms            | 0000000000000001 | True   | Default   | Default         | 10             | 1           | Throughput  | 16           | 6           |  12.60 ns |   0.051 ns |   0.030 ns |  12.60 ns |    12.66 ns |         - |
| NowUtc                              | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default         | Default        | Default     | Default     | 16           | Default     |  16.12 ns |   0.022 ns |   0.021 ns |  16.12 ns |    16.15 ns |         - |
| NowUtc                              | Net10     | 250ms            | 0000000000000001 | True   | Default   | Default         | 10             | 1           | Throughput  | 16           | 6           |  16.78 ns |   0.057 ns |   0.034 ns |  16.76 ns |    16.83 ns |         - |
| UnixMillisecondsNow                 | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default         | Default        | Default     | Default     | 16           | Default     |  23.48 ns |   0.041 ns |   0.038 ns |  23.47 ns |    23.54 ns |         - |
| EpochMillisecondsNow                | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default         | Default        | Default     | Default     | 16           | Default     |  23.67 ns |   0.047 ns |   0.041 ns |  23.65 ns |    23.76 ns |         - |
| UnixMillisecondsNow                 | Net10     | 250ms            | 0000000000000001 | True   | Default   | Default         | 10             | 1           | Throughput  | 16           | 6           |  24.49 ns |   0.046 ns |   0.027 ns |  24.48 ns |    24.55 ns |         - |
| EpochMillisecondsNow                | Net10     | 250ms            | 0000000000000001 | True   | Default   | Default         | 10             | 1           | Throughput  | 16           | 6           |  24.75 ns |   0.077 ns |   0.046 ns |  24.75 ns |    24.81 ns |         - |
| SynchronizeUnixMilliseconds_WithRtt | Net10     | 250ms            | 0000000000000001 | True   | Default   | 1               | 10             | 1           | Throughput  | 1            | 6           |  87.50 ns | 202.796 ns | 106.066 ns |  50.00 ns |   350.00 ns |         - |
| SynchronizeUnixMilliseconds_WithRtt | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | 1               | Default        | Default     | Default     | 1            | Default     | 853.12 ns |  26.108 ns |  75.328 ns | 800.00 ns | 1,000.00 ns |         - |
