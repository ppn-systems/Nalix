```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]    : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  Net10     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

PowerPlanMode=8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  Runtime=.NET 10.0  

```
| Method                           | Job       | MinIterationTime | Affinity         | Server | Toolchain | IterationCount | LaunchCount | RunStrategy | WarmupCount | PayloadBytes | Mean       | Error     | StdDev    | P100       | Gen0   | Allocated |
|--------------------------------- |---------- |----------------- |----------------- |------- |---------- |--------------- |------------ |------------ |------------ |------------- |-----------:|----------:|----------:|-----------:|-------:|----------:|
| SymmetricEngine_Encrypt_Envelope | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           |   270.8 ns |   3.91 ns |   2.33 ns |   274.3 ns |      - |         - |
| SymmetricEngine_Decrypt_Envelope | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           |   275.4 ns |   3.84 ns |   2.54 ns |   278.4 ns | 0.0019 |      24 B |
| SymmetricEngine_Encrypt_Envelope | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           |   276.8 ns |   4.91 ns |   4.60 ns |   285.5 ns |      - |         - |
| SymmetricEngine_Decrypt_Envelope | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           |   288.3 ns |   5.56 ns |   5.46 ns |   295.1 ns | 0.0019 |      24 B |
| AeadEngine_Encrypt               | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           | 1,166.5 ns |   9.94 ns |   5.91 ns | 1,175.9 ns |      - |         - |
| AeadEngine_Encrypt               | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           | 1,176.5 ns |  23.21 ns |  23.83 ns | 1,207.9 ns |      - |         - |
| AeadEngine_Decrypt               | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 64           | 1,202.6 ns |  20.82 ns |  19.48 ns | 1,231.2 ns | 0.0019 |      24 B |
| AeadEngine_Decrypt               | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 64           | 1,693.1 ns | 363.19 ns | 189.96 ns | 1,795.2 ns |      - |      24 B |
| SymmetricEngine_Encrypt_Envelope | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         | 2,730.2 ns |  19.23 ns |  11.44 ns | 2,751.4 ns |      - |         - |
| SymmetricEngine_Decrypt_Envelope | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         | 2,749.0 ns |  26.85 ns |  14.05 ns | 2,774.2 ns |      - |      24 B |
| SymmetricEngine_Encrypt_Envelope | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         | 2,810.7 ns |  52.64 ns |  56.32 ns | 2,904.6 ns |      - |         - |
| SymmetricEngine_Decrypt_Envelope | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         | 2,821.3 ns |  55.45 ns |  68.10 ns | 2,934.9 ns |      - |      24 B |
| AeadEngine_Encrypt               | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         | 6,410.1 ns |  41.61 ns |  24.76 ns | 6,450.8 ns |      - |         - |
| AeadEngine_Decrypt               | Net10     | 250ms            | 0000000000000001 | True   | Default   | 10             | 1           | Throughput  | 6           | 1024         | 6,470.1 ns |  59.67 ns |  39.47 ns | 6,542.3 ns |      - |      24 B |
| AeadEngine_Encrypt               | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         | 6,650.1 ns |  89.97 ns |  84.16 ns | 6,762.2 ns |      - |         - |
| AeadEngine_Decrypt               | .NET 10.0 | Default          | 1111111111111111 | False  | net10.0   | Default        | Default     | Default     | Default     | 1024         | 6,653.8 ns | 124.01 ns | 115.99 ns | 6,792.4 ns |      - |      24 B |
