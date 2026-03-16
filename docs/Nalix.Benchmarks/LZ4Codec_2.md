```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]    : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  Toolchain=net10.0  

```
| Method                                                                | PayloadSize | Compressible | Mean         | Error     | StdDev    | Median       | Min          | Max          | Rank | Gen0   | Allocated |
|---------------------------------------------------------------------- |------------ |------------- |-------------:|----------:|----------:|-------------:|-------------:|-------------:|-----:|-------:|----------:|
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | False        |     14.20 ns |  0.313 ns |  0.625 ns |     14.45 ns |     12.66 ns |     14.90 ns |    1 |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | False        |     30.99 ns |  0.638 ns |  1.413 ns |     30.80 ns |     26.86 ns |     34.26 ns |    2 | 0.0121 |     152 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | False        |     32.53 ns |  0.010 ns |  0.009 ns |     32.54 ns |     32.51 ns |     32.55 ns |    2 |      - |         - |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | True         |     86.84 ns |  0.028 ns |  0.023 ns |     86.85 ns |     86.79 ns |     86.88 ns |    3 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | True         |     90.68 ns |  1.832 ns |  4.920 ns |     91.45 ns |     47.55 ns |     91.52 ns |    4 |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | True         |     92.75 ns |  0.241 ns |  0.213 ns |     92.74 ns |     92.44 ns |     93.07 ns |    4 |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | False        |     98.76 ns |  2.001 ns |  3.949 ns |     98.44 ns |     77.92 ns |    102.98 ns |    5 | 0.0835 |    1048 B |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | True         |    114.49 ns |  0.052 ns |  0.049 ns |    114.50 ns |    114.40 ns |    114.58 ns |    6 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | True         |    116.62 ns |  0.438 ns |  0.410 ns |    116.55 ns |    115.67 ns |    117.40 ns |    6 | 0.0050 |      64 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | True         |    117.10 ns |  1.236 ns |  1.156 ns |    117.05 ns |    115.00 ns |    119.00 ns |    6 | 0.0120 |     152 B |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | True         |    121.03 ns |  0.028 ns |  0.026 ns |    121.04 ns |    120.98 ns |    121.07 ns |    7 |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | False        |    139.71 ns |  0.272 ns |  0.254 ns |    139.70 ns |    139.30 ns |    140.17 ns |    8 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | True         |    143.43 ns |  0.391 ns |  0.365 ns |    143.50 ns |    142.38 ns |    143.82 ns |    9 | 0.0050 |      64 B |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | False        |    316.91 ns | 19.994 ns | 58.322 ns |    342.40 ns |    154.19 ns |    362.41 ns |   10 |      - |         - |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | False        |    334.88 ns |  6.615 ns | 14.096 ns |    342.00 ns |    277.66 ns |    349.84 ns |   10 |      - |         - |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | True         |    356.52 ns |  0.095 ns |  0.089 ns |    356.54 ns |    356.34 ns |    356.65 ns |   11 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | True         |    359.27 ns |  5.900 ns |  5.230 ns |    361.30 ns |    345.72 ns |    361.46 ns |   11 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | False        |    376.30 ns |  7.543 ns | 19.740 ns |    381.42 ns |    251.83 ns |    404.01 ns |   12 | 0.0134 |     168 B |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | True         |    397.31 ns |  0.980 ns |  0.917 ns |    397.29 ns |    396.00 ns |    399.06 ns |   13 | 0.0076 |      96 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | True         |    600.26 ns |  0.994 ns |  0.930 ns |    599.90 ns |    599.17 ns |    601.96 ns |   14 |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | False        |    681.16 ns |  8.794 ns |  8.226 ns |    681.41 ns |    654.73 ns |    688.68 ns |   15 | 0.6542 |    8216 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | True         |    682.37 ns |  3.974 ns |  3.523 ns |    682.66 ns |    672.31 ns |    686.93 ns |   15 | 0.0834 |    1048 B |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | False        |  2,847.31 ns |  8.071 ns |  6.739 ns |  2,845.26 ns |  2,840.26 ns |  2,862.97 ns |   16 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | False        |  2,890.14 ns | 56.633 ns | 58.158 ns |  2,887.38 ns |  2,809.21 ns |  3,031.43 ns |   16 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | False        |  3,040.10 ns | 11.198 ns | 10.475 ns |  3,037.14 ns |  3,026.87 ns |  3,056.87 ns |   17 | 0.0839 |    1064 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | True         |  4,695.16 ns |  7.878 ns |  7.369 ns |  4,691.91 ns |  4,687.13 ns |  4,713.76 ns |   18 |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | True         |  5,263.84 ns | 43.205 ns | 40.414 ns |  5,264.71 ns |  5,158.78 ns |  5,316.45 ns |   19 | 0.6523 |    8216 B |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | False        | 23,774.54 ns |  8.244 ns |  7.308 ns | 23,776.35 ns | 23,756.30 ns | 23,786.33 ns |   20 |      - |         - |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | False        | 23,844.26 ns | 15.235 ns | 14.251 ns | 23,843.39 ns | 23,822.50 ns | 23,874.50 ns |   20 |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | False        | 25,030.89 ns | 37.958 ns | 35.506 ns | 25,029.11 ns | 24,966.63 ns | 25,102.11 ns |   21 | 0.6561 |    8264 B |
