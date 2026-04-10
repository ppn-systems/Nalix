```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method            | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | ItemCount | Mean      | Error    | StdDev    | Median    | P100      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |---------- |----------:|---------:|----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Serialize_Array   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16        |  12.72 ns | 0.237 ns |  0.222 ns |  12.76 ns |  13.00 ns |  1.00 |    0.02 | 0.0076 |      96 B |        1.00 |
| Deserialize_Array | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16        |  45.08 ns | 0.526 ns |  0.492 ns |  45.12 ns |  45.99 ns |  3.54 |    0.07 | 0.0070 |      88 B |        0.92 |
|                   |           |                  |                  |        |           |                |             |             |             |           |           |          |           |           |           |       |         |        |           |             |
| Serialize_Array   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16        |  13.21 ns | 0.296 ns |  0.196 ns |  13.22 ns |  13.45 ns |  1.00 |    0.02 | 0.0076 |      96 B |        1.00 |
| Deserialize_Array | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16        |  45.75 ns | 6.193 ns |  4.096 ns |  43.40 ns |  52.33 ns |  3.46 |    0.30 | 0.0069 |      88 B |        0.92 |
|                   |           |                  |                  |        |           |                |             |             |             |           |           |          |           |           |           |       |         |        |           |             |
| Serialize_Array   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128       |  32.85 ns | 0.672 ns |  0.660 ns |  32.99 ns |  33.32 ns |  1.00 |    0.03 | 0.0433 |     544 B |        1.00 |
| Deserialize_Array | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 128       |  68.40 ns | 1.226 ns |  1.024 ns |  68.37 ns |  70.43 ns |  2.08 |    0.05 | 0.0427 |     536 B |        0.99 |
|                   |           |                  |                  |        |           |                |             |             |             |           |           |          |           |           |           |       |         |        |           |             |
| Serialize_Array   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128       |  34.60 ns | 1.288 ns |  0.852 ns |  34.79 ns |  35.33 ns |  1.00 |    0.03 | 0.0433 |     544 B |        1.00 |
| Deserialize_Array | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 128       |  72.54 ns | 6.859 ns |  4.537 ns |  69.92 ns |  78.50 ns |  2.10 |    0.14 | 0.0427 |     536 B |        0.99 |
|                   |           |                  |                  |        |           |                |             |             |             |           |           |          |           |           |           |       |         |        |           |             |
| Serialize_Array   | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024      | 205.01 ns | 4.147 ns |  4.258 ns | 206.22 ns | 207.60 ns |  1.00 |    0.03 | 0.3278 |    4128 B |        1.00 |
| Deserialize_Array | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024      | 228.34 ns | 4.601 ns | 12.040 ns | 222.25 ns | 275.93 ns |  1.11 |    0.06 | 0.3281 |    4120 B |        1.00 |
|                   |           |                  |                  |        |           |                |             |             |             |           |           |          |           |           |           |       |         |        |           |             |
| Serialize_Array   | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024      | 156.12 ns | 5.679 ns |  3.757 ns | 154.25 ns | 162.07 ns |  1.00 |    0.03 | 0.3276 |    4128 B |        1.00 |
| Deserialize_Array | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024      | 182.18 ns | 3.032 ns |  1.804 ns | 182.96 ns | 183.84 ns |  1.17 |    0.03 | 0.3281 |    4120 B |        1.00 |
