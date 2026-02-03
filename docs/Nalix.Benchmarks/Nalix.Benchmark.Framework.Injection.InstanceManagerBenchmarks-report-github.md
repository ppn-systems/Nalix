```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]   : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=8  LaunchCount=1  
WarmupCount=3  

```
| Method                                             | InvocationCount | UnrollFactor | PreloadCount | Mean          | Error         | StdDev        | Median        | Gen0   | Allocated |
|--------------------------------------------------- |---------------- |------------- |------------- |--------------:|--------------:|--------------:|--------------:|-------:|----------:|
| **&#39;GetExisting&lt;T&gt; — generic slot hit&#39;**                | **Default**         | **16**           | **10**           |      **1.827 ns** |     **0.1517 ns** |     **0.0793 ns** |      **1.795 ns** |      **-** |         **-** |
| &#39;GetExisting&lt;T&gt; — thread-local L1 hit&#39;             | Default         | 16           | 10           |      6.315 ns |     3.7190 ns |     1.9451 ns |      7.450 ns |      - |         - |
| &#39;GetExisting&lt;T&gt; — dict fallback&#39;                   | Default         | 16           | 10           |      5.663 ns |     0.1451 ns |     0.0644 ns |      5.673 ns |      - |         - |
| **&#39;GetExisting&lt;T&gt; — generic slot hit&#39;**                | **Default**         | **16**           | **50**           |      **7.063 ns** |     **2.6701 ns** |     **1.3965 ns** |      **7.123 ns** |      **-** |         **-** |
| &#39;GetExisting&lt;T&gt; — thread-local L1 hit&#39;             | Default         | 16           | 50           |      4.062 ns |     5.7147 ns |     2.9889 ns |      2.252 ns |      - |         - |
| &#39;GetExisting&lt;T&gt; — dict fallback&#39;                   | Default         | 16           | 50           |      8.476 ns |     4.9295 ns |     2.5782 ns |      9.132 ns |      - |         - |
| **&#39;GetOrCreate&lt;T&gt; — generic slot hit (fastest path)&#39;** | **Default**         | **16**           | **10**           |      **7.166 ns** |     **3.7504 ns** |     **1.6652 ns** |      **7.880 ns** |      **-** |         **-** |
| &#39;GetOrCreate(Type) — dict hit, no args&#39;            | Default         | 16           | 10           |    200.260 ns |   130.1656 ns |    68.0791 ns |    221.329 ns | 0.0050 |      64 B |
| &#39;GetOrCreate(Type, args) — signature cache hit&#39;    | Default         | 16           | 10           |    306.464 ns |   161.0616 ns |    71.5123 ns |    274.061 ns | 0.0050 |      64 B |
| **&#39;GetOrCreate&lt;T&gt; — generic slot hit (fastest path)&#39;** | **Default**         | **16**           | **50**           |     **10.494 ns** |     **5.0122 ns** |     **2.6215 ns** |      **9.822 ns** |      **-** |         **-** |
| &#39;GetOrCreate(Type) — dict hit, no args&#39;            | Default         | 16           | 50           |    219.968 ns |    21.9184 ns |     9.7319 ns |    221.044 ns | 0.0050 |      64 B |
| &#39;GetOrCreate(Type, args) — signature cache hit&#39;    | Default         | 16           | 50           |    264.964 ns |     8.0644 ns |     2.8758 ns |    264.066 ns | 0.0050 |      64 B |
| **&#39;Register&lt;T&gt; — replace existing&#39;**                   | **1**               | **1**            | **10**           |  **4,375.000 ns** | **3,279.5491 ns** | **1,715.2676 ns** |  **3,450.000 ns** |      **-** |     **472 B** |
| &#39;Register&lt;T&gt; — with interface slots&#39;               | 1               | 1            | 10           |  7,228.571 ns | 1,638.1677 ns |   727.3566 ns |  7,000.000 ns |      - |    1032 B |
| &#39;Register&lt;T&gt; — class only (no interfaces)&#39;         | 1               | 1            | 10           |  4,342.857 ns | 2,883.8312 ns | 1,280.4389 ns |  4,400.000 ns |      - |     416 B |
| **&#39;Register&lt;T&gt; — replace existing&#39;**                   | **1**               | **1**            | **50**           |  **5,887.500 ns** | **4,352.1066 ns** | **2,276.2359 ns** |  **4,500.000 ns** |      **-** |     **472 B** |
| &#39;Register&lt;T&gt; — with interface slots&#39;               | 1               | 1            | 50           | 10,371.429 ns | 1,905.3765 ns |   845.9990 ns | 10,000.000 ns |      - |    1032 B |
| &#39;Register&lt;T&gt; — class only (no interfaces)&#39;         | 1               | 1            | 50           |  3,500.000 ns |   900.8883 ns |   400.0000 ns |  3,400.000 ns |      - |     416 B |
