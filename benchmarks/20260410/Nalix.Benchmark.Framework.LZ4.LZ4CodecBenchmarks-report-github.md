```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method         | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | PayloadBytes | Mean       | Error    | StdDev   | P100       | Gen0   | Allocated |
|--------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |------------- |-----------:|---------:|---------:|-----------:|-------:|----------:|
| Encode_ToSpan  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         |   118.2 ns |  0.71 ns |  0.42 ns |   118.7 ns |      - |         - |
| Encode_ToSpan  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         |   122.0 ns |  2.45 ns |  3.36 ns |   126.4 ns |      - |         - |
| Encode_ToLease | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         |   154.1 ns |  2.94 ns |  1.94 ns |   158.0 ns | 0.0038 |      48 B |
| Encode_ToLease | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         |   155.5 ns |  3.03 ns |  3.36 ns |   160.9 ns | 0.0038 |      48 B |
| Decode_ToSpan  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         |   296.9 ns |  5.84 ns |  6.25 ns |   305.7 ns |      - |         - |
| Decode_ToSpan  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         |   301.6 ns |  0.92 ns |  0.55 ns |   302.4 ns |      - |         - |
| Decode_ToLease | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         |   326.9 ns |  1.95 ns |  1.16 ns |   329.2 ns | 0.0038 |      48 B |
| Decode_ToLease | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         |   327.4 ns |  4.40 ns |  4.11 ns |   330.7 ns | 0.0038 |      48 B |
| Encode_ToSpan  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16384        | 1,194.8 ns |  5.91 ns |  5.52 ns | 1,202.6 ns |      - |         - |
| Encode_ToSpan  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16384        | 1,233.0 ns |  4.35 ns |  2.59 ns | 1,236.3 ns |      - |         - |
| Encode_ToLease | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16384        | 1,661.1 ns |  5.54 ns |  4.91 ns | 1,669.0 ns | 0.0038 |      48 B |
| Encode_ToLease | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16384        | 1,736.5 ns | 13.47 ns |  8.91 ns | 1,749.5 ns | 0.0038 |      48 B |
| Decode_ToSpan  | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16384        | 4,661.1 ns | 66.55 ns | 59.00 ns | 4,742.1 ns |      - |         - |
| Decode_ToSpan  | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16384        | 4,698.1 ns | 14.95 ns |  8.90 ns | 4,710.6 ns |      - |         - |
| Decode_ToLease | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 16384        | 4,734.8 ns | 75.51 ns | 66.94 ns | 4,819.6 ns |      - |      48 B |
| Decode_ToLease | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 16384        | 4,777.1 ns | 15.49 ns |  9.22 ns | 4,795.9 ns |      - |      48 B |
