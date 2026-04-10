```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method      | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | Length | Mean      | Error    | StdDev   | P100      | Gen0   | Allocated |
|------------ |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |------- |----------:|---------:|---------:|----------:|-------:|----------:|
| NextUInt64  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32     |  44.83 ns | 0.305 ns | 0.181 ns |  45.12 ns |      - |         - |
| NextUInt64  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024   |  45.04 ns | 0.369 ns | 0.220 ns |  45.53 ns |      - |         - |
| NextUInt64  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32     |  45.28 ns | 0.901 ns | 0.884 ns |  46.83 ns |      - |         - |
| NextUInt64  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024   |  45.83 ns | 0.709 ns | 0.629 ns |  46.79 ns |      - |         - |
| CreateNonce | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024   |  52.24 ns | 0.892 ns | 0.531 ns |  53.33 ns | 0.0031 |      40 B |
| CreateNonce | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32     |  52.37 ns | 0.873 ns | 0.577 ns |  53.40 ns | 0.0031 |      40 B |
| CreateNonce | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024   |  53.27 ns | 0.918 ns | 0.858 ns |  54.69 ns | 0.0032 |      40 B |
| CreateNonce | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32     |  53.38 ns | 1.014 ns | 0.949 ns |  54.74 ns | 0.0032 |      40 B |
| Fill        | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32     |  62.37 ns | 0.244 ns | 0.145 ns |  62.64 ns |      - |         - |
| Fill        | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32     |  64.22 ns | 1.261 ns | 1.295 ns |  66.55 ns |      - |         - |
| GetBytes    | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 32     |  67.63 ns | 1.081 ns | 0.715 ns |  68.95 ns | 0.0043 |      56 B |
| GetBytes    | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 32     |  67.90 ns | 1.360 ns | 1.566 ns |  70.35 ns | 0.0044 |      56 B |
| Fill        | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024   | 249.49 ns | 1.511 ns | 0.899 ns | 250.63 ns |      - |         - |
| Fill        | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024   | 250.81 ns | 4.744 ns | 4.659 ns | 257.57 ns |      - |         - |
| GetBytes    | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024   | 296.97 ns | 4.807 ns | 2.861 ns | 300.67 ns | 0.0830 |    1048 B |
| GetBytes    | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024   | 301.05 ns | 4.546 ns | 4.253 ns | 308.00 ns | 0.0834 |    1048 B |
