```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method                    | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | ChunkCount | Mean       | Error     | StdDev     | P100       | Gen0   | Allocated |
|-------------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |----------- |-----------:|----------:|-----------:|-----------:|-------:|----------:|
| IsFragmentedFrame         | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4          |   2.568 ns | 0.0508 ns |  0.0302 ns |   2.601 ns |      - |         - |
| IsFragmentedFrame         | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16         |   2.570 ns | 0.0586 ns |  0.0388 ns |   2.617 ns |      - |         - |
| IsFragmentedFrame         | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16         |   2.704 ns | 0.0771 ns |  0.1029 ns |   2.918 ns |      - |         - |
| IsFragmentedFrame         | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4          |   2.705 ns | 0.0750 ns |  0.0736 ns |   2.775 ns |      - |         - |
| Assemble_SequentialChunks | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4          | 107.271 ns | 2.1165 ns |  2.5993 ns | 111.380 ns | 0.0293 |     368 B |
| Assemble_SequentialChunks | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4          | 114.802 ns | 2.2404 ns |  1.4819 ns | 117.190 ns | 0.0286 |     368 B |
| Assemble_SequentialChunks | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16         | 319.349 ns | 6.3917 ns | 15.5583 ns | 363.371 ns | 0.0291 |     368 B |
| Assemble_SequentialChunks | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16         | 333.648 ns | 2.8383 ns |  1.8773 ns | 336.975 ns | 0.0267 |     368 B |
