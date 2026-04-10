```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method           | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | ItemCount | Mean      | Error    | StdDev   | P100      | Allocated |
|----------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |---------- |----------:|---------:|---------:|----------:|----------:|
| Trim             | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256       |  24.54 ns | 0.087 ns | 0.073 ns |  24.70 ns |         - |
| Trim             | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32        |  24.69 ns | 0.073 ns | 0.068 ns |  24.79 ns |         - |
| Trim             | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256       |  25.54 ns | 0.112 ns | 0.067 ns |  25.63 ns |         - |
| Trim             | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32        |  25.57 ns | 0.212 ns | 0.126 ns |  25.84 ns |         - |
| Rent_Fill_Return | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32        |  70.75 ns | 3.935 ns | 2.603 ns |  74.60 ns |         - |
| Rent_Fill_Return | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32        |  77.18 ns | 0.864 ns | 0.808 ns |  78.79 ns |         - |
| Rent_Fill_Return | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256       | 178.80 ns | 3.108 ns | 2.907 ns | 183.42 ns |         - |
| Rent_Fill_Return | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256       | 186.52 ns | 2.576 ns | 1.704 ns | 189.05 ns |         - |
