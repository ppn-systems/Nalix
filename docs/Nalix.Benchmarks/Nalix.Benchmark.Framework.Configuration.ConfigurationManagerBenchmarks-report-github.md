```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=8  LaunchCount=1  
WarmupCount=3  

```
| Method                                                    | InvocationCount | UnrollFactor | Mean           | Error          | StdDev         | Gen0   | Allocated |
|---------------------------------------------------------- |---------------- |------------- |---------------:|---------------:|---------------:|-------:|----------:|
| &#39;Get&lt;T&gt; — cache hit (Lazy already computed)&#39;              | Default         | 16           |      32.914 ns |      1.0996 ns |      0.4883 ns | 0.0025 |      32 B |
| &#39;Get&lt;T&gt; — cache miss (first load, file I/O)&#39;              | 1               | 1            |   7,162.500 ns |  1,312.5261 ns |    686.4765 ns |      - |     472 B |
| &#39;IsLoaded&lt;T&gt; — type present (ContainsKey hit)&#39;            | Default         | 16           |       6.729 ns |      0.4685 ns |      0.2451 ns |      - |         - |
| &#39;IsLoaded&lt;T&gt; — type absent (ContainsKey miss)&#39;            | Default         | 16           |       5.517 ns |      0.3629 ns |      0.1611 ns |      - |         - |
| &#39;ReloadAll — 2 loaded containers (file I/O + write lock)&#39; | Default         | 16           |  68,426.635 ns | 14,364.1380 ns |  7,512.7219 ns | 1.0376 |   13440 B |
| &#39;SetConfigFilePath — no reload (path swap only)&#39;          | 1               | 1            |  60,157.143 ns | 11,411.6402 ns |  5,066.8390 ns |      - |    1272 B |
| &#39;SetConfigFilePath — with auto-reload (file I/O)&#39;         | 1               | 1            | 149,712.500 ns | 25,271.2492 ns | 13,217.3520 ns |      - |   15432 B |
