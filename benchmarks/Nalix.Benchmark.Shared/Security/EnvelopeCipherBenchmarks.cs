// Copyright (c) 2026 PPN Corporation. All rights reserved.

//13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
//.NET SDK 10.0.103
//  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 [AttachedDebugger]
//DefaultJob: .NET 10.0.3(10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


// | Method                 | PayloadSize | Algorithm         | Mean       | Error | Allocated |
// |----------------------- |------------ |------------------ |-----------:|------:|----------:|
// | EnvelopeCipher.Encrypt | 128         | XTEA              |   9.500 us |    NA |     360 B |
// | EnvelopeCipher.Decrypt | 128         | XTEA              |  12.200 us |    NA |     608 B |
// | EnvelopeCipher.Encrypt | 128         | SPECK             |   4.400 us |    NA |     696 B |
// | EnvelopeCipher.Decrypt | 128         | SPECK             |   8.800 us |    NA |     928 B |
// | EnvelopeCipher.Encrypt | 128         | SALSA20           |   5.100 us |    NA |     360 B |
// | EnvelopeCipher.Decrypt | 128         | SALSA20           |   8.700 us |    NA |     608 B |
// | EnvelopeCipher.Encrypt | 128         | CHACHA20          |   4.400 us |    NA |     776 B |
// | EnvelopeCipher.Decrypt | 128         | CHACHA20          |   8.900 us |    NA |    1016 B |
// | EnvelopeCipher.Encrypt | 128         | XTEA_POLY1305     |  10.200 us |    NA |     680 B |
// | EnvelopeCipher.Decrypt | 128         | XTEA_POLY1305     |  14.200 us |    NA |     904 B |
// | EnvelopeCipher.Encrypt | 128         | SPECK_POLY1305    |  26.400 us |    NA |    1656 B |
// | EnvelopeCipher.Decrypt | 128         | SPECK_POLY1305    |  21.700 us |    NA |    1872 B |
// | EnvelopeCipher.Encrypt | 128         | SALSA20_POLY1305  |  12.100 us |    NA |     680 B |
// | EnvelopeCipher.Decrypt | 128         | SALSA20_POLY1305  |  21.900 us |    NA |     904 B |
// | EnvelopeCipher.Encrypt | 128         | CHACHA20_POLY1305 |  12.600 us |    NA |    1304 B |
// | EnvelopeCipher.Decrypt | 128         | CHACHA20_POLY1305 |  77.600 us |    NA |    1528 B |
// | EnvelopeCipher.Encrypt | 1024        | XTEA              |  25.500 us |    NA |    2152 B |
// | EnvelopeCipher.Decrypt | 1024        | XTEA              |  26.300 us |    NA |    1504 B |
// | EnvelopeCipher.Encrypt | 1024        | SPECK             |   6.800 us |    NA |    2488 B |
// | EnvelopeCipher.Decrypt | 1024        | SPECK             |  41.500 us |    NA |    1824 B |
// | EnvelopeCipher.Encrypt | 1024        | SALSA20           |   8.300 us |    NA |    2152 B |
// | EnvelopeCipher.Decrypt | 1024        | SALSA20           |  10.000 us |    NA |    1504 B |
// | EnvelopeCipher.Encrypt | 1024        | CHACHA20          |   8.900 us |    NA |    2568 B |
// | EnvelopeCipher.Decrypt | 1024        | CHACHA20          |  38.700 us |    NA |    1912 B |
// | EnvelopeCipher.Encrypt | 1024        | XTEA_POLY1305     |  39.600 us |    NA |    2472 B |
// | EnvelopeCipher.Decrypt | 1024        | XTEA_POLY1305     |  63.300 us |    NA |    1800 B |
// | EnvelopeCipher.Encrypt | 1024        | SPECK_POLY1305    |  21.600 us |    NA |    3448 B |
// | EnvelopeCipher.Decrypt | 1024        | SPECK_POLY1305    |  18.800 us |    NA |    2768 B |
// | EnvelopeCipher.Encrypt | 1024        | SALSA20_POLY1305  |  19.900 us |    NA |    2472 B |
// | EnvelopeCipher.Decrypt | 1024        | SALSA20_POLY1305  |  24.100 us |    NA |    1800 B |
// | EnvelopeCipher.Encrypt | 1024        | CHACHA20_POLY1305 |  21.700 us |    NA |    3096 B |
// | EnvelopeCipher.Decrypt | 1024        | CHACHA20_POLY1305 |  23.200 us |    NA |    2424 B |
// | EnvelopeCipher.Encrypt | 8192        | XTEA              | 150.600 us |    NA |   16488 B |
// | EnvelopeCipher.Decrypt | 8192        | XTEA              | 164.900 us |    NA |    8672 B |
// | EnvelopeCipher.Encrypt | 8192        | SPECK             |  32.600 us |    NA |   16824 B |
// | EnvelopeCipher.Decrypt | 8192        | SPECK             |  30.500 us |    NA |    8992 B |
// | EnvelopeCipher.Encrypt | 8192        | SALSA20           |  28.400 us |    NA |   16488 B |
// | EnvelopeCipher.Decrypt | 8192        | SALSA20           |  32.800 us |    NA |    8672 B |
// | EnvelopeCipher.Encrypt | 8192        | CHACHA20          |  50.200 us |    NA |   16904 B |
// | EnvelopeCipher.Decrypt | 8192        | CHACHA20          |  54.400 us |    NA |    9080 B |
// | EnvelopeCipher.Encrypt | 8192        | XTEA_POLY1305     | 231.300 us |    NA |   16808 B |
// | EnvelopeCipher.Decrypt | 8192        | XTEA_POLY1305     | 271.500 us |    NA |    8968 B |
// | EnvelopeCipher.Encrypt | 8192        | SPECK_POLY1305    |  74.200 us |    NA |   17784 B |
// | EnvelopeCipher.Decrypt | 8192        | SPECK_POLY1305    |  56.100 us |    NA |    9936 B |
// | EnvelopeCipher.Encrypt | 8192        | SALSA20_POLY1305  |  54.300 us |    NA |   16808 B |
// | EnvelopeCipher.Decrypt | 8192        | SALSA20_POLY1305  |  79.900 us |    NA |    8968 B |
// | EnvelopeCipher.Encrypt | 8192        | CHACHA20_POLY1305 |  73.750 us |    NA |   17432 B |
// | EnvelopeCipher.Decrypt | 8192        | CHACHA20_POLY1305 |  72.000 us |    NA |    9592 B | 

using BenchmarkDotNet.Attributes;
using Nalix.Common.Enums;
using Nalix.Shared.Security;
using System;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Shared.Security;

// Memory diagnoser to capture allocations.
[SimpleJob(
    BenchmarkDotNet.Engines.RunStrategy.Throughput,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1,
    launchCount: 1)]
[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
public class EnvelopeCipherBenchmarks
{
    // Vary payload sizes to observe scaling behavior.
    [Params(128, 1024, 8192)]
    public Int32 PayloadSize;

    // Test both an AEAD and a stream/CTR cipher to compare behavior.
    [Params(CipherSuiteType.XTEA, CipherSuiteType.SPECK, CipherSuiteType.SALSA20, CipherSuiteType.CHACHA20,
            CipherSuiteType.XTEA_POLY1305, CipherSuiteType.SPECK_POLY1305, CipherSuiteType.SALSA20_POLY1305, CipherSuiteType.CHACHA20_POLY1305)]
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