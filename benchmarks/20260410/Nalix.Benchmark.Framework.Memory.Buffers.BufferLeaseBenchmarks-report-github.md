```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method              | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | PayloadBytes | Mean     | Error    | StdDev   | P100     | Gen0   | Allocated |
|-------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |------------- |---------:|---------:|---------:|---------:|-------:|----------:|
| CopyFrom_Dispose    | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128          | 23.30 ns | 0.319 ns | 0.190 ns | 23.54 ns | 0.0038 |      48 B |
| Rent_Commit_Dispose | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128          | 23.39 ns | 0.253 ns | 0.151 ns | 23.52 ns | 0.0038 |      48 B |
| Rent_Commit_Dispose | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128          | 23.65 ns | 0.415 ns | 0.388 ns | 24.21 ns | 0.0038 |      48 B |
| CopyFrom_Dispose    | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128          | 24.29 ns | 0.500 ns | 0.491 ns | 24.98 ns | 0.0038 |      48 B |
| Rent_Commit_Dispose | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 2048         | 37.47 ns | 0.373 ns | 0.222 ns | 37.78 ns | 0.0038 |      48 B |
| CopyFrom_Dispose    | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 2048         | 41.24 ns | 0.316 ns | 0.188 ns | 41.65 ns | 0.0038 |      48 B |
| CopyFrom_Dispose    | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 2048         | 87.59 ns | 2.699 ns | 7.478 ns | 90.10 ns | 0.0038 |      48 B |
| Rent_Commit_Dispose | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 2048         | 93.85 ns | 1.820 ns | 2.235 ns | 95.82 ns | 0.0038 |      48 B |
