```log

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.103
  [Host]    : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  Toolchain=net10.0  

```

| Method                 | PayloadSize | Algorithm         | Mean        | Error       | StdDev      | Median      | Min         | Max         | Rank | Code Size | Gen0   | Allocated |
|----------------------- |------------ |------------------ |------------:|------------:|------------:|------------:|------------:|------------:|-----:|----------:|-------:|----------:|
| EnvelopeCipher.Decrypt | 128         | SALSA20           |    395.9 ns |    28.14 ns |    82.98 ns |    407.5 ns |    292.5 ns |    537.2 ns |    1 |   2,100 B | 0.0038 |      48 B |
| EnvelopeCipher.Decrypt | 128         | CHACHA20          |    506.4 ns |     8.92 ns |     8.34 ns |    506.8 ns |    484.3 ns |    519.1 ns |    2 |   2,100 B | 0.0038 |      48 B |
| EnvelopeCipher.Encrypt | 128         | SALSA20           |    587.4 ns |    11.63 ns |    26.48 ns |    600.8 ns |    457.6 ns |    610.8 ns |    3 |     893 B |      - |         - |
| EnvelopeCipher.Encrypt | 128         | CHACHA20          |    611.3 ns |    11.28 ns |    10.55 ns |    607.2 ns |    595.6 ns |    631.0 ns |    3 |     893 B |      - |         - |
| EnvelopeCipher.Decrypt | 128         | SALSA20_POLY1305  |    742.0 ns |    14.15 ns |    12.54 ns |    745.3 ns |    717.8 ns |    759.7 ns |    4 |   7,624 B | 0.0038 |      48 B |
| EnvelopeCipher.Decrypt | 128         | CHACHA20_POLY1305 |    837.0 ns |    14.97 ns |    14.00 ns |    839.2 ns |    789.8 ns |    849.6 ns |    5 |   7,748 B | 0.0038 |      48 B |
| EnvelopeCipher.Encrypt | 128         | SALSA20_POLY1305  |  1,159.6 ns |    22.66 ns |    26.10 ns |  1,161.4 ns |  1,109.6 ns |  1,204.8 ns |    6 |     893 B |      - |         - |
| EnvelopeCipher.Encrypt | 128         | CHACHA20_POLY1305 |  1,572.9 ns |    22.72 ns |    18.98 ns |  1,573.2 ns |  1,537.6 ns |  1,594.5 ns |    7 |     893 B |      - |         - |
| EnvelopeCipher.Decrypt | 1024        | SALSA20           |  2,075.4 ns |    10.63 ns |     9.43 ns |  2,076.6 ns |  2,055.5 ns |  2,088.8 ns |    8 |   2,100 B | 0.0038 |      48 B |
| EnvelopeCipher.Encrypt | 1024        | SALSA20           |  2,103.5 ns |    30.22 ns |    28.27 ns |  2,113.8 ns |  2,025.1 ns |  2,129.7 ns |    8 |     893 B |      - |         - |
| EnvelopeCipher.Decrypt | 1024        | CHACHA20          |  3,114.3 ns |    58.89 ns |    63.02 ns |  3,130.8 ns |  2,987.1 ns |  3,201.0 ns |    9 |   2,100 B | 0.0038 |      48 B |
| EnvelopeCipher.Decrypt | 1024        | SALSA20_POLY1305  |  3,573.7 ns |    69.23 ns |    94.77 ns |  3,602.2 ns |  3,357.0 ns |  3,714.3 ns |   10 |   7,624 B | 0.0038 |      48 B |
| EnvelopeCipher.Encrypt | 1024        | CHACHA20          |  4,671.8 ns |   559.37 ns | 1,649.32 ns |  3,405.1 ns |  2,953.8 ns |  7,170.9 ns |   10 |     893 B |      - |         - |
| EnvelopeCipher.Decrypt | 1024        | CHACHA20_POLY1305 |  5,447.6 ns |    13.62 ns |    12.74 ns |  5,449.3 ns |  5,412.0 ns |  5,467.8 ns |   11 |   7,748 B | 0.0038 |      48 B |
| EnvelopeCipher.Encrypt | 1024        | SALSA20_POLY1305  |  5,837.9 ns |   115.74 ns |   137.78 ns |  5,829.7 ns |  5,559.4 ns |  6,099.7 ns |   12 |     893 B |      - |         - |
| EnvelopeCipher.Encrypt | 1024        | CHACHA20_POLY1305 | 13,182.2 ns |   260.79 ns |   639.73 ns | 13,319.9 ns | 10,760.6 ns | 14,192.7 ns |   13 |     893 B |      - |         - |
| EnvelopeCipher.Decrypt | 8192        | SALSA20           | 25,199.7 ns |   504.22 ns | 1,363.19 ns | 25,484.1 ns | 16,641.5 ns | 25,994.3 ns |   14 |   2,100 B |      - |      48 B |
| EnvelopeCipher.Encrypt | 8192        | SALSA20           | 25,484.2 ns |   290.35 ns |   271.59 ns | 25,578.3 ns | 24,738.5 ns | 25,727.2 ns |   14 |     893 B |      - |         - |
| EnvelopeCipher.Decrypt | 8192        | CHACHA20_POLY1305 | 40,680.4 ns |   803.75 ns |   893.36 ns | 40,961.0 ns | 37,807.2 ns | 41,409.5 ns |   15 |   7,748 B |      - |      48 B |
| EnvelopeCipher.Decrypt | 8192        | SALSA20_POLY1305  | 44,694.3 ns |   890.52 ns | 1,878.41 ns | 44,868.3 ns | 39,508.2 ns | 48,296.5 ns |   16 |   7,624 B |      - |      48 B |
| EnvelopeCipher.Decrypt | 8192        | CHACHA20          | 51,150.2 ns |   988.84 ns | 1,510.07 ns | 51,117.2 ns | 47,311.3 ns | 54,144.6 ns |   17 |   2,100 B |      - |      48 B |
| EnvelopeCipher.Encrypt | 8192        | CHACHA20          | 51,269.9 ns |   946.22 ns |   885.09 ns | 51,048.5 ns | 49,804.8 ns | 52,710.6 ns |   17 |     893 B |      - |         - |
| EnvelopeCipher.Encrypt | 8192        | SALSA20_POLY1305  | 69,966.1 ns | 1,347.01 ns | 1,322.95 ns | 70,501.7 ns | 65,680.3 ns | 70,668.4 ns |   18 |     893 B |      - |         - |
| EnvelopeCipher.Encrypt | 8192        | CHACHA20_POLY1305 | 93,320.3 ns | 1,838.27 ns | 2,807.23 ns | 93,701.9 ns | 86,490.1 ns | 98,520.6 ns |   19 |     893 B |      - |         - |
