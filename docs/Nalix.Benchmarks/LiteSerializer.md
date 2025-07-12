```log

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]    : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  Toolchain=net10.0  

```

| Method                                                     | ArrayLength | Mean        | Error     | StdDev     | Median      | Min         | Max         | Rank | Code Size | Gen0   | Allocated |
|----------------------------------------------------------- |------------ |------------:|----------:|-----------:|------------:|------------:|------------:|-----:|----------:|-------:|----------:|
| &#39;Serialize&lt;int&gt; -&gt; byte[]&#39;                                 | 1           |   0.0428 ns | 0.0006 ns |  0.0006 ns |   0.0428 ns |   0.0420 ns |   0.0440 ns |    1 |   9,058 B | 0.0000 |         - |
| &#39;Serialize&lt;int&gt; -&gt; byte[]&#39;                                 | 256         |   0.0447 ns | 0.0010 ns |  0.0009 ns |   0.0448 ns |   0.0435 ns |   0.0461 ns |    1 |   9,058 B | 0.0000 |         - |
| &#39;Serialize&lt;LargeStruct&gt; -&gt; existing byte[] buffer&#39;         | 1           |   0.0491 ns | 0.0007 ns |  0.0007 ns |   0.0490 ns |   0.0477 ns |   0.0504 ns |    2 |   8,202 B |      - |         - |
| &#39;Serialize&lt;LargeStruct&gt; -&gt; existing byte[] buffer&#39;         | 2048        |   0.0772 ns | 0.0021 ns |  0.0030 ns |   0.0767 ns |   0.0720 ns |   0.0835 ns |    3 |   8,202 B |      - |         - |
| &#39;Deserialize&lt;int&gt; &lt;- ReadOnlySpan&lt;byte&gt; (ref)&#39;             | 256         |   0.0773 ns | 0.0007 ns |  0.0006 ns |   0.0774 ns |   0.0762 ns |   0.0784 ns |    3 |  16,120 B | 0.0000 |         - |
| &#39;Serialize&lt;LargeStruct&gt; -&gt; existing byte[] buffer&#39;         | 256         |   0.0784 ns | 0.0021 ns |  0.0032 ns |   0.0781 ns |   0.0720 ns |   0.0853 ns |    3 |   8,202 B |      - |         - |
| &#39;Deserialize&lt;int&gt; &lt;- ReadOnlySpan&lt;byte&gt; (ref)&#39;             | 1           |   0.0793 ns | 0.0011 ns |  0.0011 ns |   0.0792 ns |   0.0773 ns |   0.0811 ns |    3 |  16,120 B | 0.0000 |         - |
| &#39;Serialize&lt;int&gt; -&gt; byte[]&#39;                                 | 2048        |   0.0802 ns | 0.0018 ns |  0.0018 ns |   0.0803 ns |   0.0759 ns |   0.0832 ns |    3 |   9,058 B | 0.0000 |         - |
| &#39;Serialize&lt;LargeStruct&gt; -&gt; byte[]&#39;                         | 1           |   0.1231 ns | 0.0019 ns |  0.0018 ns |   0.1227 ns |   0.1199 ns |   0.1255 ns |    4 |   9,144 B | 0.0001 |       1 B |
| &#39;Deserialize&lt;int&gt; &lt;- ReadOnlySpan&lt;byte&gt; (ref)&#39;             | 2048        |   0.1302 ns | 0.0028 ns |  0.0048 ns |   0.1309 ns |   0.1107 ns |   0.1369 ns |    5 |  16,120 B | 0.0000 |         - |
| &#39;Serialize&lt;LargeStruct&gt; -&gt; byte[]&#39;                         | 2048        |   0.2097 ns | 0.0081 ns |  0.0230 ns |   0.2148 ns |   0.1165 ns |   0.2347 ns |    6 |   9,144 B | 0.0001 |       1 B |
| &#39;Deserialize&lt;LargeStruct&gt; &lt;- ReadOnlySpan&lt;byte&gt; (ref)&#39;     | 1           |   0.2138 ns | 0.0032 ns |  0.0030 ns |   0.2141 ns |   0.2085 ns |   0.2199 ns |    6 |  16,319 B | 0.0001 |       1 B |
| &#39;Serialize&lt;LargeStruct&gt; -&gt; byte[]&#39;                         | 256         |   0.2295 ns | 0.0152 ns |  0.0427 ns |   0.2443 ns |   0.1239 ns |   0.2755 ns |    6 |   9,144 B | 0.0001 |       1 B |
| &#39;Deserialize&lt;LargeStruct&gt; &lt;- ReadOnlySpan&lt;byte&gt; (ref)&#39;     | 2048        |   0.2582 ns | 0.0098 ns |  0.0267 ns |   0.2502 ns |   0.2236 ns |   0.3556 ns |    6 |  16,319 B | 0.0001 |       1 B |
| &#39;Deserialize&lt;LargeStruct&gt; &lt;- ReadOnlySpan&lt;byte&gt; (ref)&#39;     | 256         |   0.3635 ns | 0.0044 ns |  0.0037 ns |   0.3637 ns |   0.3532 ns |   0.3672 ns |    7 |  16,319 B | 0.0001 |       1 B |
| &#39;Serialize&lt;int[]&gt; -&gt; byte[]&#39;                               | 1           |   0.5622 ns | 0.0099 ns |  0.0093 ns |   0.5622 ns |   0.5497 ns |   0.5768 ns |    8 |   2,232 B | 0.0002 |       2 B |
| &#39;Serialize&lt;int[]&gt; -&gt; byte[]&#39;                               | 256         |   6.1070 ns | 0.0492 ns |  0.0436 ns |   6.1058 ns |   6.0182 ns |   6.1619 ns |    9 |   2,232 B | 0.0053 |      66 B |
| &#39;Serialize&lt;int[]&gt; -&gt; byte[]&#39;                               | 2048        |  17.3832 ns | 0.3430 ns |  0.6929 ns |  17.2481 ns |  15.9099 ns |  18.9940 ns |   10 |   2,232 B | 0.0408 |     514 B |
| &#39;Deserialize&lt;int[]&gt; &lt;- ReadOnlySpan&lt;byte&gt; (out bytesRead)&#39; | 1           |  31.1692 ns | 0.5025 ns |  0.4700 ns |  31.0823 ns |  30.5256 ns |  32.2261 ns |   11 |   3,394 B | 0.0025 |      32 B |
| &#39;Deserialize&lt;int[]&gt; &lt;- BufferLease&#39;                        | 1           |  32.2864 ns | 0.3588 ns |  0.3356 ns |  32.2862 ns |  31.8083 ns |  32.8540 ns |   11 |   3,527 B | 0.0025 |      32 B |
| &#39;Serialize&lt;int[]&gt; -&gt; BufferLease (rent)&#39;                   | 1           |  80.9214 ns | 1.2613 ns |  1.1798 ns |  81.1537 ns |  78.7428 ns |  82.6020 ns |   12 |   7,872 B | 0.0038 |      48 B |
| &#39;Deserialize&lt;int[]&gt; &lt;- BufferLease&#39;                        | 256         | 132.6581 ns | 2.8344 ns |  8.1326 ns | 134.1751 ns |  98.6845 ns | 147.8646 ns |   13 |   3,527 B | 0.0834 |    1048 B |
| &#39;Deserialize&lt;int[]&gt; &lt;- ReadOnlySpan&lt;byte&gt; (out bytesRead)&#39; | 256         | 133.0238 ns | 2.6542 ns |  4.7861 ns | 132.9720 ns | 120.6511 ns | 140.2290 ns |   13 |   3,394 B | 0.0834 |    1048 B |
| &#39;Serialize&lt;int[]&gt; -&gt; BufferLease (rent)&#39;                   | 256         | 152.1825 ns | 2.5200 ns |  2.3572 ns | 152.7962 ns | 145.6089 ns | 154.3198 ns |   14 |   7,872 B | 0.0038 |      48 B |
| &#39;Serialize&lt;int[]&gt; -&gt; BufferLease (rent)&#39;                   | 2048        | 212.5017 ns | 4.2485 ns |  5.3730 ns | 212.8725 ns | 200.3685 ns | 223.5051 ns |   15 |   7,872 B | 0.0038 |      48 B |
| &#39;Deserialize&lt;int[]&gt; &lt;- ReadOnlySpan&lt;byte&gt; (out bytesRead)&#39; | 2048        | 316.7738 ns | 6.2976 ns | 13.4207 ns | 315.4970 ns | 290.9536 ns | 351.6424 ns |   16 |   3,394 B | 0.6542 |    8216 B |
| &#39;Deserialize&lt;int[]&gt; &lt;- BufferLease&#39;                        | 2048        | 328.7137 ns | 6.4408 ns | 15.4317 ns | 328.9579 ns | 304.1967 ns | 367.9813 ns |   16 |   3,527 B | 0.6542 |    8216 B |
