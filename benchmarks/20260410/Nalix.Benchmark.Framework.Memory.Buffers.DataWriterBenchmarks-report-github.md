```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method                 | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | PayloadBytes | Mean       | Error     | StdDev    | P100       | Allocated |
|----------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |------------- |-----------:|----------:|----------:|-----------:|----------:|
| Write_WithFixedArray   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128          |   2.383 ns | 0.0721 ns | 0.0859 ns |   2.525 ns |         - |
| Write_WithFixedArray   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128          |   2.496 ns | 0.0117 ns | 0.0070 ns |   2.505 ns |         - |
| Write_WithRentedBuffer | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128          |  10.050 ns | 0.0600 ns | 0.0397 ns |  10.098 ns |         - |
| Write_WithRentedBuffer | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128          |  10.349 ns | 0.2015 ns | 0.1885 ns |  10.589 ns |         - |
| Expand_ThenWrite       | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128          |  21.281 ns | 0.4428 ns | 0.5099 ns |  21.956 ns |         - |
| Expand_ThenWrite       | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128          |  21.675 ns | 0.3362 ns | 0.2224 ns |  22.027 ns |         - |
| Write_WithFixedArray   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096         |  28.395 ns | 0.5825 ns | 0.5721 ns |  28.974 ns |         - |
| Write_WithRentedBuffer | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096         |  53.080 ns | 0.6193 ns | 0.5490 ns |  53.826 ns |         - |
| Write_WithRentedBuffer | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096         |  55.288 ns | 0.2355 ns | 0.1401 ns |  55.547 ns |         - |
| Expand_ThenWrite       | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096         |  66.436 ns | 0.5689 ns | 0.3385 ns |  66.770 ns |         - |
| Expand_ThenWrite       | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096         |  67.553 ns | 0.7012 ns | 0.6559 ns |  68.138 ns |         - |
| Write_WithFixedArray   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096         | 111.164 ns | 0.3522 ns | 0.2096 ns | 111.476 ns |         - |
