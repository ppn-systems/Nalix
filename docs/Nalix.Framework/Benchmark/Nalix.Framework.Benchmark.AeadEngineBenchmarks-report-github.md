```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26100.6725/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host] : .NET 9.0.10 (9.0.10, 9.0.1025.47515), X64 RyuJIT x86-64-v3 [AttachedDebugger]
  .NET 9 : .NET 9.0.10 (9.0.10, 9.0.1025.47515), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=10  
WarmupCount=3  

```
| Method  | Categories | Algorithm        | PayloadSize | Mean           | Error        | StdDev       | P95            | Ratio | Gen0    | Completed Work Items | Lock Contentions | Gen1   | Allocated | Alloc Ratio |
|-------- |----------- |----------------- |------------ |---------------:|-------------:|-------------:|---------------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| Decrypt | Decrypt    | XteaPoly1305     | 0           |     1,295.7 ns |     13.10 ns |      8.66 ns |     1,307.3 ns |     ? |  0.0286 |                    - |                - |      - |     360 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | XteaPoly1305     | 64          |     1,352.5 ns |    333.43 ns |    220.54 ns |     1,476.9 ns |     ? |  0.0334 |                    - |                - |      - |     424 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | XteaPoly1305     | 1024        |     3,354.5 ns |  1,358.37 ns |    898.48 ns |     4,127.0 ns |     ? |  0.1068 |                    - |                - |      - |    1384 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | XteaPoly1305     | 65536       |   174,506.4 ns |  2,274.68 ns |  1,504.56 ns |   175,973.7 ns |     ? |  5.1270 |                    - |                - |      - |   65898 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | XteaPoly1305     | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | SpeckPoly1305    | 0           |       817.8 ns |     33.11 ns |     21.90 ns |       849.9 ns |     ? |  0.1011 |                    - |                - |      - |    1280 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | SpeckPoly1305    | 64          |       748.3 ns |     31.27 ns |     18.61 ns |       771.2 ns |     ? |  0.1068 |                    - |                - |      - |    1344 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | SpeckPoly1305    | 1024        |     4,742.4 ns |    103.90 ns |     68.72 ns |     4,824.7 ns |     ? |  0.1831 |                    - |                - |      - |    2304 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | SpeckPoly1305    | 65536       |   269,399.4 ns |  8,093.61 ns |  5,353.42 ns |   277,533.6 ns |     ? |  4.8828 |                    - |                - |      - |   66818 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | SpeckPoly1305    | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | Salsa20Poly1305  | 0           |       392.2 ns |     14.30 ns |      9.46 ns |       401.9 ns |     ? |  0.0286 |                    - |                - |      - |     360 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | Salsa20Poly1305  | 64          |       655.6 ns |     19.96 ns |     13.20 ns |       675.6 ns |     ? |  0.0334 |                    - |                - |      - |     424 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | Salsa20Poly1305  | 1024        |     4,561.0 ns |    107.20 ns |     70.91 ns |     4,653.5 ns |     ? |  0.1068 |                    - |                - |      - |    1384 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | Salsa20Poly1305  | 65536       |   274,074.3 ns |  8,480.27 ns |  5,609.18 ns |   282,444.2 ns |     ? |  4.8828 |                    - |                - |      - |   65898 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | Salsa20Poly1305  | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | ChaCha20Poly1305 | 0           |       690.0 ns |    101.11 ns |     60.17 ns |       770.6 ns |     ? |  0.0954 |                    - |                - |      - |    1200 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | ChaCha20Poly1305 | 64          |     1,004.1 ns |     45.96 ns |     30.40 ns |     1,049.7 ns |     ? |  0.0992 |                    - |                - |      - |    1264 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | ChaCha20Poly1305 | 1024        |     6,243.7 ns |    174.88 ns |    115.67 ns |     6,352.9 ns |     ? |  0.1755 |                    - |                - |      - |    2224 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | ChaCha20Poly1305 | 65536       |   347,294.6 ns |  8,561.67 ns |  5,663.02 ns |   353,620.5 ns |     ? |  4.8828 |                    - |                - |      - |   66738 B |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Decrypt | Decrypt    | ChaCha20Poly1305 | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | XteaPoly1305     | 0           |       886.2 ns |     23.93 ns |     15.83 ns |       903.8 ns |  1.00 |  0.0343 |                    - |                - |      - |     440 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | XteaPoly1305     | 64          |     2,225.1 ns |     41.67 ns |     27.57 ns |     2,261.4 ns |  1.00 |  0.0420 |                    - |                - |      - |     568 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | XteaPoly1305     | 1024        |    27,163.5 ns |  8,189.45 ns |  5,416.81 ns |    32,741.7 ns |  1.04 |  0.1831 |                    - |                - |      - |    2488 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | XteaPoly1305     | 65536       | 1,986,346.7 ns | 41,959.60 ns | 27,753.68 ns | 2,010,731.3 ns |  1.00 |  9.7656 |                    - |                - |      - |  131512 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | XteaPoly1305     | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | SpeckPoly1305    | 0           |       884.7 ns |     76.35 ns |     50.50 ns |       913.0 ns |  1.00 |  0.1087 |                    - |                - |      - |    1368 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | SpeckPoly1305    | 64          |     1,207.6 ns |    493.57 ns |    326.46 ns |     1,494.1 ns |  1.08 |  0.1183 |                    - |                - |      - |    1496 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | SpeckPoly1305    | 1024        |     5,037.3 ns |    108.16 ns |     71.54 ns |     5,116.0 ns |  1.00 |  0.2670 |                    - |                - |      - |    3416 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | SpeckPoly1305    | 65536       |   285,676.5 ns |  5,296.09 ns |  3,503.04 ns |   290,190.3 ns |  1.00 | 10.2539 |                    - |                - |      - |  132440 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | SpeckPoly1305    | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | Salsa20Poly1305  | 0           |       707.1 ns |     26.25 ns |     17.36 ns |       728.3 ns |  1.00 |  0.0343 |                    - |                - |      - |     440 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | Salsa20Poly1305  | 64          |     1,258.4 ns |      9.06 ns |      5.99 ns |     1,265.7 ns |  1.00 |  0.0439 |                    - |                - |      - |     568 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | Salsa20Poly1305  | 1024        |     9,375.0 ns |     82.99 ns |     54.89 ns |     9,446.8 ns |  1.00 |  0.1831 |                    - |                - |      - |    2488 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | Salsa20Poly1305  | 65536       |   552,632.8 ns |  4,757.06 ns |  3,146.50 ns |   555,136.5 ns |  1.00 |  9.7656 |                    - |                - | 0.9766 |  131512 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | Salsa20Poly1305  | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | ChaCha20Poly1305 | 0           |     1,025.9 ns |     17.29 ns |     10.29 ns |     1,036.3 ns |  1.00 |  0.1011 |                    - |                - |      - |    1280 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | ChaCha20Poly1305 | 64          |     1,872.1 ns |     69.87 ns |     46.21 ns |     1,931.2 ns |  1.00 |  0.1106 |                    - |                - |      - |    1408 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | ChaCha20Poly1305 | 1024        |    13,940.9 ns |    309.08 ns |    204.44 ns |    14,146.0 ns |  1.00 |  0.2594 |                    - |                - |      - |    3328 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | ChaCha20Poly1305 | 65536       |   828,712.3 ns | 14,240.39 ns |  9,419.14 ns |   838,221.4 ns |  1.00 |  9.7656 |                    - |                - |      - |  132352 B |        1.00 |
|         |            |                  |             |                |              |              |                |       |         |                      |                  |        |           |             |
| Encrypt | Encrypt    | ChaCha20Poly1305 | 1048576     |             NA |           NA |           NA |             NA |     ? |      NA |                   NA |               NA |     NA |        NA |           ? |
