```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  Job-OIOVDM : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

IterationCount=3  RunStrategy=ColdStart  WarmupCount=1  

```
| Method                        | Mean     | Error     | StdDev    | Gen0   | Allocated |
|------------------------------ |---------:|----------:|----------:|-------:|----------:|
| HighVolume_10k                | 2.317 ms | 0.8801 ms | 0.0482 ms | 0.2000 |   2.69 KB |
| Concurrent_MultiThread        | 2.265 ms | 9.7733 ms | 0.5357 ms | 0.2000 |   2.69 KB |
| SustainedLoad_HeldConnections | 3.407 ms | 0.0216 ms | 0.0012 ms |      - |   2.68 KB |
| RapidReconnect_NoSleep        | 2.138 ms | 0.0791 ms | 0.0043 ms |      - |      3 KB |
