```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  Job-BMPORG : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=10  LaunchCount=1  
WarmupCount=3  

```
| Method                                                                | PayloadSize | Compressible | Mean          | Error         | StdDev        | Gen0   | Gen1   | Allocated |
|---------------------------------------------------------------------- |------------ |------------- |--------------:|--------------:|--------------:|-------:|-------:|----------:|
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **128**         | **False**        |  **3,498.206 ns** |    **20.4183 ns** |    **13.5054 ns** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | False        |  3,451.587 ns |     4.6055 ns |     2.7406 ns |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | False        |  3,487.464 ns |     7.7008 ns |     5.0936 ns | 0.0267 |      - |     344 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | False        |      6.538 ns |     0.3443 ns |     0.2278 ns |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | False        |     13.868 ns |     0.4960 ns |     0.2594 ns | 0.0121 |      - |     152 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **128**         | **True**         |  **3,314.322 ns** |    **11.2781 ns** |     **7.4598 ns** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | True         |  3,313.669 ns |    21.0856 ns |    12.5477 ns |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | True         |  6,866.739 ns | 2,525.4376 ns | 1,670.4206 ns | 0.0191 |      - |     240 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | True         |     89.795 ns |     9.0044 ns |     5.9558 ns |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | True         |    106.293 ns |    13.6529 ns |     9.0305 ns | 0.0120 |      - |     152 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **1024**        | **False**        | **10,580.632 ns** | **3,429.8667 ns** | **2,268.6445 ns** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | False        | 11,014.391 ns |   641.9419 ns |   424.6048 ns |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | False        | 11,400.355 ns |   825.8842 ns |   546.2713 ns | 0.1678 |      - |    2144 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | False        |     26.823 ns |     1.9004 ns |     1.2570 ns |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | False        |     95.183 ns |    20.8661 ns |    13.8016 ns | 0.0835 |      - |    1048 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **1024**        | **True**         |  **7,602.290 ns** |   **602.3673 ns** |   **398.4287 ns** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | True         |  7,634.366 ns |   821.3027 ns |   543.2409 ns |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | True         |  8,084.855 ns |   580.0928 ns |   383.6955 ns | 0.0877 |      - |    1144 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | True         |    622.944 ns |    41.1959 ns |    24.5150 ns |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | True         |    466.416 ns |    73.1566 ns |    38.2623 ns | 0.0834 |      - |    1048 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **8192**        | **False**        | **16,080.583 ns** |   **895.1339 ns** |   **592.0756 ns** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | False        | 16,401.620 ns |   852.8654 ns |   564.1177 ns |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | False        | 16,562.127 ns |   130.3020 ns |    68.1505 ns | 1.3123 | 0.0305 |   16536 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | False        |     67.311 ns |     3.3687 ns |     2.2282 ns |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | False        |    275.909 ns |     7.0558 ns |     4.1988 ns | 0.6542 |      - |    8216 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **8192**        | **True**         |  **3,451.976 ns** |    **10.6515 ns** |     **7.0453 ns** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | True         |  3,446.299 ns |    13.0148 ns |     8.6085 ns |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | True         |  3,732.389 ns |    20.8665 ns |    10.9136 ns | 0.6638 |      - |    8368 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | True         |  2,997.614 ns |   107.9584 ns |    71.4078 ns |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | True         |  3,393.047 ns |   131.3537 ns |    86.8823 ns | 0.6523 |      - |    8216 B |
