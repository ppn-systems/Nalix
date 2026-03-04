// Copyright (c) 2026 PPN Corporation. All rights reserved.

// 13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
// .NET SDK 10.0.103
//   [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
//   DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


// | Method                 | PayloadSize | Algorithm         | Mean        | Error     | StdDev      | Gen0   | Gen1   | Allocated |
// |----------------------- |------------ |------------------ |------------:|----------:|------------:|-------:|-------:|----------:|
// | EnvelopeCipher.Encrypt | 128         | SPECK             |    425.4 ns |   2.01 ns |     1.78 ns | 0.0553 |      - |     696 B |
// | EnvelopeCipher.Decrypt | 128         | SPECK             |    304.9 ns |   6.12 ns |     7.05 ns | 0.0410 |      - |     520 B |
// | EnvelopeCipher.Encrypt | 128         | SALSA20           |    417.5 ns |   7.82 ns |     7.32 ns | 0.0286 |      - |     360 B |
// | EnvelopeCipher.Decrypt | 128         | SALSA20           |    288.0 ns |   5.65 ns |     7.73 ns | 0.0157 |      - |     200 B |
// | EnvelopeCipher.Encrypt | 128         | CHACHA20          |    621.0 ns |  12.28 ns |    13.13 ns | 0.0362 |      - |     464 B |
// | EnvelopeCipher.Decrypt | 128         | CHACHA20          |    501.9 ns |  10.08 ns |    14.77 ns | 0.0229 |      - |     296 B |
// | EnvelopeCipher.Encrypt | 128         | SPECK_POLY1305    |  1,379.5 ns |  26.71 ns |    37.44 ns | 0.0668 |      - |     840 B |
// | EnvelopeCipher.Decrypt | 128         | SPECK_POLY1305    |  1,322.4 ns |  25.77 ns |    36.96 ns | 0.0668 |      - |     840 B |
// | EnvelopeCipher.Encrypt | 128         | SALSA20_POLY1305  |  1,280.3 ns |  25.17 ns |    30.91 ns | 0.0153 |      - |     192 B |
// | EnvelopeCipher.Decrypt | 128         | SALSA20_POLY1305  |  1,219.7 ns |  24.34 ns |    33.31 ns | 0.0153 |      - |     200 B |
// | EnvelopeCipher.Encrypt | 128         | CHACHA20_POLY1305 |  1,689.1 ns |  32.74 ns |    41.40 ns | 0.0153 |      - |     192 B |
// | EnvelopeCipher.Decrypt | 128         | CHACHA20_POLY1305 |  1,645.2 ns |  32.22 ns |    33.09 ns | 0.0153 |      - |     200 B |
// | EnvelopeCipher.Encrypt | 1024        | SPECK             |  2,148.9 ns |  42.54 ns |    48.99 ns | 0.1945 |      - |    2488 B |
// | EnvelopeCipher.Decrypt | 1024        | SPECK             |  1,725.8 ns |  31.41 ns |    29.38 ns | 0.1125 |      - |    1416 B |
// | EnvelopeCipher.Encrypt | 1024        | SALSA20           |  2,458.5 ns |  48.70 ns |    61.59 ns | 0.1678 |      - |    2152 B |
// | EnvelopeCipher.Decrypt | 1024        | SALSA20           |  2,005.2 ns |  40.14 ns |    60.07 ns | 0.0839 |      - |    1096 B |
// | EnvelopeCipher.Encrypt | 1024        | CHACHA20          |  3,391.0 ns |  65.49 ns |    61.26 ns | 0.1793 |      - |    2256 B |
// | EnvelopeCipher.Decrypt | 1024        | CHACHA20          |  2,980.1 ns |  58.70 ns |    74.23 ns | 0.0916 |      - |    1192 B |
// | EnvelopeCipher.Encrypt | 1024        | SPECK_POLY1305    |  5,606.5 ns | 108.03 ns |   120.08 ns | 0.1373 |      - |    1736 B |
// | EnvelopeCipher.Decrypt | 1024        | SPECK_POLY1305    |  5,541.6 ns | 110.47 ns |   131.51 ns | 0.1373 |      - |    1736 B |
// | EnvelopeCipher.Encrypt | 1024        | SALSA20_POLY1305  |  5,760.4 ns | 111.05 ns |   136.38 ns | 0.0839 |      - |    1088 B |
// | EnvelopeCipher.Decrypt | 1024        | SALSA20_POLY1305  |  5,764.8 ns |  97.43 ns |    91.13 ns | 0.0839 |      - |    1096 B |
// | EnvelopeCipher.Encrypt | 1024        | CHACHA20_POLY1305 |  6,854.2 ns | 136.43 ns |   167.55 ns | 0.0839 |      - |    1088 B |
// | EnvelopeCipher.Decrypt | 1024        | CHACHA20_POLY1305 |  6,871.4 ns | 127.94 ns |   131.38 ns | 0.0839 |      - |    1096 B |
// | EnvelopeCipher.Encrypt | 8192        | SPECK             | 15,830.7 ns | 314.07 ns |   308.46 ns | 1.3123 | 0.0305 |   16824 B |
// | EnvelopeCipher.Decrypt | 8192        | SPECK             | 12,804.0 ns | 248.03 ns |   322.51 ns | 0.6714 | 0.0153 |    8584 B |
// | EnvelopeCipher.Encrypt | 8192        | SALSA20           | 18,682.3 ns | 370.80 ns |   427.02 ns | 1.3123 | 0.0305 |   16488 B |
// | EnvelopeCipher.Decrypt | 8192        | SALSA20           | 15,530.9 ns | 307.32 ns |   377.42 ns | 0.6409 |      - |    8264 B |
// | EnvelopeCipher.Encrypt | 8192        | CHACHA20          | 26,043.3 ns | 386.58 ns |   361.61 ns | 1.3123 | 0.0305 |   16592 B |
// | EnvelopeCipher.Decrypt | 8192        | CHACHA20          | 22,835.0 ns | 445.42 ns |   624.42 ns | 0.6409 |      - |    8360 B |
// | EnvelopeCipher.Encrypt | 8192        | SPECK_POLY1305    | 39,608.2 ns | 764.71 ns | 1,072.02 ns | 0.6714 |      - |    8904 B |
// | EnvelopeCipher.Decrypt | 8192        | SPECK_POLY1305    | 39,104.1 ns | 659.78 ns |   584.88 ns | 0.6714 |      - |    8904 B |
// | EnvelopeCipher.Encrypt | 8192        | SALSA20_POLY1305  | 40,734.9 ns | 772.68 ns |   793.48 ns | 0.6104 |      - |    8256 B |
// | EnvelopeCipher.Decrypt | 8192        | SALSA20_POLY1305  | 41,613.3 ns | 795.92 ns |   947.48 ns | 0.6104 |      - |    8264 B |
// | EnvelopeCipher.Encrypt | 8192        | CHACHA20_POLY1305 | 48,734.3 ns | 957.05 ns | 1,341.64 ns | 0.6104 |      - |    8256 B |
// | EnvelopeCipher.Decrypt | 8192        | CHACHA20_POLY1305 | 49,632.1 ns | 980.41 ns | 1,129.04 ns | 0.6104 |      - |    8264 B |

using BenchmarkDotNet.Attributes;
using Nalix.Common.Enums;
using Nalix.Shared.Security;
using System;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Shared.Security;

// Memory diagnoser to capture allocations.
[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
public class EnvelopeCipherBenchmarks
{
    // Vary payload sizes to observe scaling behavior.
    [Params(128, 1024, 8192)]
    public Int32 PayloadSize;

    // Test both an AEAD and a stream/CTR cipher to compare behavior.
    [Params(CipherSuiteType.SALSA20, CipherSuiteType.CHACHA20,
            CipherSuiteType.SALSA20_POLY1305, CipherSuiteType.CHACHA20_POLY1305)]
    public CipherSuiteType Algorithm;

    private Byte[] _key = Array.Empty<Byte>();
    private Byte[] _aad = Array.Empty<Byte>();
    private Byte[] _plaintext = Array.Empty<Byte>();
    private Byte[] _envelope = Array.Empty<Byte>();

    // Global setup runs once per parameter combination.
    [GlobalSetup]
    public void GlobalSetup()
    {
        // Create a 32-byte key (required for CHACHA20/CHACHA20_POLY1305).
        _key = new Byte[32];
        RandomNumberGenerator.Fill(_key);

        // AAD used for AEAD suites; ignored for stream suites.
        _aad = new Byte[16];
        RandomNumberGenerator.Fill(_aad);

        // Create random plaintext of the selected size.
        _plaintext = new Byte[PayloadSize];
        RandomNumberGenerator.Fill(_plaintext);

        // Pre-encrypt once so Decrypt benchmark operates only on the decrypt path.
        // Use AsSpan to match EnvelopeCipher signatures that accept ReadOnlySpan<byte>.
        _envelope = EnvelopeCipher.Encrypt(_key.AsSpan(), _plaintext.AsSpan(), Algorithm, _aad.AsSpan());
    }

    // Benchmark: measure Encrypt performance (returns envelope to prevent optimization-out).
    [Benchmark(Description = "EnvelopeCipher.Encrypt")]
    public Byte[] Encrypt() => EnvelopeCipher.Encrypt(_key.AsSpan(), _plaintext.AsSpan(), Algorithm, _aad.AsSpan());

    // Benchmark: measure Decrypt performance (returns plaintext buffer).
    [Benchmark(Description = "EnvelopeCipher.Decrypt")]
    public Byte[] Decrypt()
    {
        // Decrypt returns a bool and outputs plaintext via out parameter.
        EnvelopeCipher.Decrypt(_key.AsSpan(), _envelope.AsSpan(), out var pt, _aad.AsSpan());
        return pt ?? Array.Empty<Byte>();
    }
}