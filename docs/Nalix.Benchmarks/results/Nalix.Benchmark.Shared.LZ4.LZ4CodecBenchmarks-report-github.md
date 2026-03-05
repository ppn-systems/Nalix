```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]    : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  Toolchain=net10.0  

```
| Method                                                                | PayloadSize | Compressible | Mean          | Error       | StdDev      | Code Size | Gen0   | Gen1   | Allocated |
|---------------------------------------------------------------------- |------------ |------------- |--------------:|------------:|------------:|----------:|-------:|-------:|----------:|
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **128**         | **False**        |  **3,460.112 ns** |   **5.8700 ns** |   **5.4908 ns** |     **598 B** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | False        |  3,443.075 ns |   4.2810 ns |   3.7950 ns |   1,196 B |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | False        |  3,478.344 ns |   7.0005 ns |   6.5483 ns |     273 B | 0.0267 |      - |     344 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | False        |      6.843 ns |   0.1599 ns |   0.3577 ns |   1,578 B |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | False        |     17.816 ns |   0.3664 ns |   0.5136 ns |     165 B | 0.0121 |      - |     152 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **128**         | **True**         |  **3,313.477 ns** |   **2.5788 ns** |   **2.4122 ns** |     **598 B** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 128         | True         |  3,310.799 ns |   5.5968 ns |   5.2352 ns |   1,196 B |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 128         | True         |  3,348.372 ns |   5.2326 ns |   4.8946 ns |     273 B | 0.0191 |      - |     240 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 128         | True         |     55.600 ns |   1.1018 ns |   1.1314 ns |   1,578 B |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 128         | True         |     65.761 ns |   1.3241 ns |   1.5249 ns |     165 B | 0.0120 |      - |     152 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **1024**        | **False**        |  **4,819.175 ns** |  **53.7136 ns** |  **50.2437 ns** |     **598 B** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | False        |  4,785.321 ns |  42.8609 ns |  40.0921 ns |   1,196 B |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | False        |  4,933.754 ns |  58.7595 ns |  54.9637 ns |     273 B | 0.1678 |      - |    2144 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | False        |     12.307 ns |   0.2694 ns |   0.5442 ns |   1,578 B |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | False        |     44.132 ns |   0.9079 ns |   1.3865 ns |     165 B | 0.0835 |      - |    1048 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **1024**        | **True**         |  **3,323.881 ns** |   **6.1389 ns** |   **5.7423 ns** |     **598 B** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 1024        | True         |  3,317.184 ns |   6.3745 ns |   5.9627 ns |   1,196 B |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 1024        | True         |  3,398.038 ns |  11.3443 ns |  10.6114 ns |     273 B | 0.0877 |      - |    1144 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 1024        | True         |    371.642 ns |   5.6331 ns |   5.2692 ns |   1,578 B |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 1024        | True         |    428.798 ns |   4.8811 ns |   4.5657 ns |     165 B | 0.0834 |      - |    1048 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **8192**        | **False**        | **16,374.822 ns** | **322.2924 ns** | **620.9481 ns** |     **598 B** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | False        | 16,428.768 ns | 327.2977 ns | 622.7179 ns |   1,196 B |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | False        | 17,250.918 ns | 340.0198 ns | 694.5704 ns |     273 B | 1.3123 | 0.0305 |   16536 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | False        |     67.758 ns |   1.3766 ns |   2.6192 ns |   1,578 B |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | False        |    319.150 ns |   6.3111 ns |  15.5995 ns |     165 B | 0.6542 |      - |    8216 B |
| **&#39;Encode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;**                              | **8192**        | **True**         |  **3,452.081 ns** |   **6.1032 ns** |   **5.4103 ns** |     **598 B** |      **-** |      **-** |         **-** |
| &#39;Encode(byte[] input, byte[] output)&#39;                                 | 8192        | True         |  3,442.789 ns |   3.1201 ns |   2.6054 ns |   1,196 B |      - |      - |         - |
| &#39;Encode(ReadOnlySpan&lt;byte&gt;) -&gt; new byte[]&#39;                            | 8192        | True         |  3,825.845 ns |  30.3440 ns |  28.3838 ns |     273 B | 0.6638 |      - |    8368 B |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;)&#39;                              | 8192        | True         |  2,961.253 ns |  59.1698 ns |  63.3110 ns |   1,578 B |      - |      - |         - |
| &#39;Decode(ReadOnlySpan&lt;byte&gt;, out byte[] output, out int bytesWritten)&#39; | 8192        | True         |  3,226.213 ns |  56.5153 ns |  52.8644 ns |     165 B | 0.6523 |      - |    8216 B |
