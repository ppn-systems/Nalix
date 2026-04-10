```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method   | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | PayloadBytes | Mean       | Error    | StdDev   | P100       | Allocated |
|--------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |------------- |-----------:|---------:|---------:|-----------:|----------:|
| Compress | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           |   250.1 ns |  1.07 ns |  0.64 ns |   251.0 ns |         - |
| Compress | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256          |   251.1 ns |  1.27 ns |  0.76 ns |   251.9 ns |         - |
| Compress | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256          |   254.7 ns |  5.07 ns |  5.63 ns |   262.6 ns |         - |
| Compress | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           |   257.2 ns |  5.09 ns |  3.98 ns |   260.1 ns |         - |
| Encrypt  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256          | 1,483.3 ns | 10.92 ns |  7.22 ns | 1,495.0 ns |         - |
| Encrypt  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256          | 1,506.0 ns | 29.92 ns | 35.62 ns | 1,557.6 ns |         - |
| Encrypt  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           | 1,508.3 ns | 11.81 ns |  6.18 ns | 1,514.7 ns |         - |
| Encrypt  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           | 1,639.5 ns | 32.75 ns | 32.17 ns | 1,686.2 ns |         - |
