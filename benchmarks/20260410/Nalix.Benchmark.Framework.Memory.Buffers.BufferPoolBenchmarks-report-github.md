```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method               | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | BufferSize | Mean       | Error     | StdDev    | P100       | Allocated |
|--------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |----------- |-----------:|----------:|----------:|-----------:|----------:|
| GetAllocationForSize | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256        |  0.3762 ns | 0.0120 ns | 0.0080 ns |  0.3855 ns |         - |
| GetAllocationForSize | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256        |  0.6119 ns | 0.0268 ns | 0.0251 ns |  0.6312 ns |         - |
| GetAllocationForSize | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096       |  3.1389 ns | 0.0823 ns | 0.0770 ns |  3.2065 ns |         - |
| GetAllocationForSize | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096       |  3.1707 ns | 0.0210 ns | 0.0139 ns |  3.1897 ns |         - |
| Rent_Return_Segment  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256        | 51.5067 ns | 0.1062 ns | 0.0942 ns | 51.6496 ns |         - |
| Rent_Return_Array    | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 256        | 51.7034 ns | 0.1137 ns | 0.1008 ns | 51.8963 ns |         - |
| Rent_Return_Segment  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096       | 52.3645 ns | 0.3575 ns | 0.3344 ns | 52.7706 ns |         - |
| Rent_Return_Array    | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 4096       | 52.8334 ns | 0.3454 ns | 0.3062 ns | 53.2526 ns |         - |
| Rent_Return_Segment  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096       | 53.2846 ns | 0.1434 ns | 0.0948 ns | 53.4860 ns |         - |
| Rent_Return_Array    | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256        | 53.4282 ns | 0.1122 ns | 0.0668 ns | 53.5221 ns |         - |
| Rent_Return_Segment  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 256        | 53.5682 ns | 0.2768 ns | 0.1831 ns | 53.8779 ns |         - |
| Rent_Return_Array    | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 4096       | 53.6839 ns | 0.2319 ns | 0.1534 ns | 54.0109 ns |         - |
