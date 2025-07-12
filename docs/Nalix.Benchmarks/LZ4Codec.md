```log

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]    : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  Toolchain=net10.0  

```

| Method                                                                | PayloadSize | Compressible | Mean          | Error       | StdDev      | Median        | Min           | Max           | Rank | Gen0   | Gen1   | Allocated |
|---------------------------------------------------------------------- |------------ |------------- |--------------:|------------:|------------:|--------------:|--------------:|--------------:|-----:|-------:|-------:|----------:|
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | False        |      7.031 ns |   0.1648 ns |   0.2201 ns |      7.096 ns |      6.695 ns |      7.334 ns |    1 |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | False        |     12.883 ns |   0.2787 ns |   0.3997 ns |     12.905 ns |     12.300 ns |     13.816 ns |    2 |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | False        |     15.520 ns |   0.3315 ns |   0.5258 ns |     15.325 ns |     14.746 ns |     16.366 ns |    3 | 0.0121 |      - |     152 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | False        |     44.719 ns |   0.9170 ns |   0.9417 ns |     44.672 ns |     42.966 ns |     46.520 ns |    4 | 0.0835 |      - |    1048 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | True         |     54.667 ns |   0.8169 ns |   0.7641 ns |     54.406 ns |     53.470 ns |     55.798 ns |    5 |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | True         |     64.107 ns |   1.2689 ns |   1.3578 ns |     63.677 ns |     62.639 ns |     67.318 ns |    6 | 0.0120 |      - |     152 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | False        |     68.656 ns |   1.3767 ns |   1.7900 ns |     69.431 ns |     65.966 ns |     70.803 ns |    7 |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | False        |    282.915 ns |   5.6520 ns |   8.9647 ns |    285.790 ns |    262.631 ns |    292.707 ns |    8 | 0.6542 |      - |    8216 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | True         |    366.737 ns |   5.6712 ns |   5.3048 ns |    363.002 ns |    361.587 ns |    376.401 ns |    9 |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | True         |    407.216 ns |   4.0975 ns |   3.8328 ns |    407.958 ns |    401.007 ns |    414.326 ns |   10 | 0.0834 |      - |    1048 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | True         |  2,866.016 ns |  42.3735 ns |  39.6362 ns |  2,841.596 ns |  2,831.282 ns |  2,933.647 ns |   11 |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | True         |  3,162.949 ns |  62.4985 ns |  61.3819 ns |  3,160.525 ns |  3,065.873 ns |  3,251.474 ns |   12 | 0.6523 |      - |    8216 B |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | True         |  3,314.859 ns |   3.4616 ns |   3.2380 ns |  3,314.639 ns |  3,309.785 ns |  3,320.079 ns |   13 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | True         |  3,316.433 ns |   1.8804 ns |   1.5702 ns |  3,316.795 ns |  3,314.745 ns |  3,319.485 ns |   13 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | True         |  3,324.149 ns |   3.2057 ns |   2.8417 ns |  3,324.518 ns |  3,318.118 ns |  3,328.265 ns |   13 |      - |      - |         - |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | True         |  3,325.592 ns |   4.7581 ns |   3.7148 ns |  3,325.138 ns |  3,319.935 ns |  3,332.933 ns |   13 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | True         |  3,350.533 ns |   6.3714 ns |   5.6481 ns |  3,351.372 ns |  3,340.597 ns |  3,358.725 ns |   13 | 0.0191 |      - |     240 B |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | True         |  3,402.100 ns |   4.1937 ns |   3.7176 ns |  3,402.139 ns |  3,395.328 ns |  3,408.410 ns |   13 | 0.0877 |      - |    1144 B |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | True         |  3,442.445 ns |   4.7711 ns |   4.2295 ns |  3,442.006 ns |  3,437.696 ns |  3,451.012 ns |   13 |      - |      - |         - |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | False        |  3,461.201 ns |  17.0559 ns |  15.9541 ns |  3,457.193 ns |  3,439.380 ns |  3,482.019 ns |   13 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | True         |  3,465.570 ns |  10.4381 ns |   9.2531 ns |  3,462.106 ns |  3,457.652 ns |  3,486.812 ns |   13 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | False        |  3,509.686 ns |  16.8279 ns |  15.7409 ns |  3,506.328 ns |  3,484.510 ns |  3,542.567 ns |   13 | 0.0267 |      - |     344 B |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | False        |  3,514.670 ns |   7.3785 ns |   6.5408 ns |  3,516.274 ns |  3,502.308 ns |  3,523.848 ns |   13 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | True         |  3,741.025 ns |  17.0602 ns |  15.9582 ns |  3,737.714 ns |  3,706.225 ns |  3,773.873 ns |   14 | 0.6638 |      - |    8368 B |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | False        |  4,794.478 ns |  34.2871 ns |  30.3946 ns |  4,792.606 ns |  4,741.758 ns |  4,854.316 ns |   15 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | False        |  4,798.100 ns |  25.9762 ns |  24.2982 ns |  4,793.861 ns |  4,767.337 ns |  4,836.426 ns |   15 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | False        |  5,256.850 ns |  99.7622 ns | 208.2408 ns |  5,179.797 ns |  4,964.715 ns |  5,737.000 ns |   16 | 0.1678 |      - |    2144 B |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | False        | 16,452.206 ns | 324.6184 ns | 422.0957 ns | 16,671.414 ns | 15,898.401 ns | 17,043.948 ns |   17 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | False        | 16,730.440 ns | 330.3923 ns | 324.4894 ns | 16,794.159 ns | 16,095.242 ns | 17,038.638 ns |   17 |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | False        | 17,203.014 ns | 339.1878 ns | 441.0399 ns | 17,286.327 ns | 16,446.582 ns | 17,807.031 ns |   17 | 1.3123 | 0.0305 |   16536 B |
