```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method             | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | ItemCount | Mean      | Error      | StdDev    | Median   | P100      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |---------- |----------:|-----------:|----------:|---------:|----------:|------:|--------:|-------:|----------:|------------:|
| Deserialize_Object | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16        |  30.19 ns |   0.619 ns |  0.688 ns | 30.57 ns |  30.91 ns |  0.49 |    0.01 | 0.0140 |     176 B |        1.57 |
| Serialize_Object   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16        |  61.11 ns |   1.242 ns |  1.220 ns | 61.50 ns |  62.35 ns |  1.00 |    0.03 | 0.0088 |     112 B |        1.00 |
|                    |           |                  |                  |        |           |                |             |             |             |           |           |            |           |          |           |       |         |        |           |             |
| Deserialize_Object | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16        |  29.90 ns |   0.253 ns |  0.167 ns | 29.95 ns |  30.13 ns |  0.37 |    0.17 | 0.0138 |     176 B |        1.57 |
| Serialize_Object   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16        | 111.36 ns | 105.612 ns | 69.856 ns | 59.37 ns | 203.93 ns |  1.38 |    1.12 | 0.0086 |     112 B |        1.00 |
|                    |           |                  |                  |        |           |                |             |             |             |           |           |            |           |          |           |       |         |        |           |             |
| Deserialize_Object | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128       |  47.97 ns |   0.971 ns |  0.997 ns | 48.11 ns |  49.69 ns |  0.59 |    0.02 | 0.0497 |     624 B |        1.11 |
| Serialize_Object   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128       |  81.00 ns |   1.628 ns |  1.742 ns | 81.47 ns |  82.89 ns |  1.00 |    0.03 | 0.0446 |     560 B |        1.00 |
|                    |           |                  |                  |        |           |                |             |             |             |           |           |            |           |          |           |       |         |        |           |             |
| Deserialize_Object | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128       |  71.21 ns |  29.158 ns | 19.286 ns | 80.86 ns |  88.75 ns |  0.85 |    0.22 | 0.0496 |     624 B |        1.11 |
| Serialize_Object   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128       |  84.26 ns |   2.671 ns |  1.589 ns | 84.21 ns |  86.97 ns |  1.00 |    0.03 | 0.0443 |     560 B |        1.00 |
