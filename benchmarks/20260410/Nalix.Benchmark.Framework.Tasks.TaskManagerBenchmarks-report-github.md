```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method                            | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | Mean          | Error       | StdDev        | P100          | Gen0   | Allocated |
|---------------------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |--------------:|------------:|--------------:|--------------:|-------:|----------:|
| RunOnceAsync_NoOp                 | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |      2.482 μs |   0.0476 μs |     0.0529 μs |      2.586 μs | 0.3510 |    4.3 KB |
| RunOnceAsync_NoOp                 | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |      3.349 μs |   0.3858 μs |     0.2296 μs |      3.837 μs | 0.1984 |    2.6 KB |
| GenerateReport_WithTrackedEntries | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |  2,402.169 μs |  48.3599 μs |   141.8313 μs |  2,612.447 μs |      - |  31.34 KB |
| GenerateReport_WithTrackedEntries | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           |  2,403.770 μs | 567.7420 μs |   375.5262 μs |  3,082.277 μs |      - |   28.6 KB |
| ScheduleWorker_NoOp_AndWait       | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     |  9,770.339 μs | 891.5757 μs | 2,628.8312 μs | 14,685.812 μs |      - |   7.73 KB |
| ScheduleWorker_NoOp_AndWait       | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 15,492.845 μs |  90.9839 μs |    54.1430 μs | 15,551.138 μs |      - |   6.61 KB |
