// Copyright (c) 2025 PPN Corporation. All rights reserved.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using Nalix.Common.Enums;
using Nalix.Framework.Cryptography.Symmetric;
using Nalix.Framework.Cryptography.Symmetric.Suite;

namespace Nalix.Framework.Benchmark;

/// <summary>
/// Benchmark suite for SymmetricEngine:
/// - Raw keystream XOR (Encrypt with dst Span)
/// - Raw one-shot (returns byte[])
/// - Envelope Encrypt (header || nonce || ciphertext)
/// - Envelope Decrypt
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[ThreadingDiagnoser]
[HideColumns(["StdErr", "Median", "RatioSD"])]
[Orderer(SummaryOrderPolicy.Declared)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
[CategoriesColumn]
[Config(typeof(BenchmarkConfig))]
public class SymmetricEngineBenchmarks
{
    // -------- Parameters --------

    [Params(
        CipherSuiteType.ChaCha20,
        CipherSuiteType.Salsa20,
        CipherSuiteType.Speck,
        CipherSuiteType.Xtea)]
    public CipherSuiteType Algorithm { get; set; }

    [Params(0, 64, 1024, 65536, 1048576)]
    public System.Int32 PayloadSize { get; set; }

    // -------- State --------

    private System.Byte[] _key = default!;
    private System.Byte[] _nonce = default!;
    private System.Byte[] _plaintext = default!;
    private System.Byte[] _dstBuffer = default!;      // preallocated dst for raw path
    private System.Byte[] _envelope = default!;       // for decrypt benchmark
    private System.UInt64 _sink;                        // anti-DCE

    [GlobalSetup]
    public void Setup()
    {
        _key = GenerateKeyFor(Algorithm);
        _nonce = GenerateNonceFor(Algorithm);
        _plaintext = CreateDeterministicBytes(PayloadSize, 0xABCDEF01u);
        _dstBuffer = new System.Byte[_plaintext.Length];

        // Pre-build an envelope for Decrypt runs (seq fixed for determinism)
        _envelope = SymmetricEngine.Encrypt(_key, _plaintext, Algorithm, nonce: default, seq: 0x11223344u);
        _sink ^= Fnv1a64(_envelope);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        System.Array.Clear(_key, 0, _key.Length);
        System.Array.Clear(_nonce, 0, _nonce.Length);
        System.Array.Clear(_plaintext, 0, _plaintext.Length);
        System.Array.Clear(_dstBuffer, 0, _dstBuffer.Length);
        System.Array.Clear(_envelope, 0, _envelope.Length);
    }

    // -------- Benchmarks --------

    [Benchmark(Description = "Raw Encrypt (dst preallocated)", Baseline = true)]
    [BenchmarkCategory("Raw")]
    public void RawEncrypt_PreallocDst()
    {
        // Counter usage: ChaCha uses low 32 bits; others use 64.
        const System.UInt64 counter = 0x55667788u;
        SymmetricEngine.Encrypt(Algorithm, _key, _nonce, counter, _plaintext, _dstBuffer);
        _sink ^= Fnv1a64(_dstBuffer);
    }

    [Benchmark(Description = "Raw Encrypt (one-shot alloc)")]
    [BenchmarkCategory("Raw")]
    public void RawEncrypt_Alloc()
    {
        const System.UInt64 counter = 0xA1B2C3D4u;
        var ct = SymmetricEngine.Encrypt(Algorithm, _key, _nonce, counter, _plaintext);
        _sink ^= Fnv1a64(ct);
    }

    [Benchmark(Description = "Envelope Encrypt")]
    [BenchmarkCategory("Envelope")]
    public void Envelope_Encrypt()
    {
        // nonce: let engine auto-generate; seq fixed for determinism
        var env = SymmetricEngine.Encrypt(_key, _plaintext, Algorithm, nonce: default, seq: 0x99AA55CCu);
        _sink ^= Fnv1a64(env);
    }

    [Benchmark(Description = "Envelope Decrypt")]
    [BenchmarkCategory("Envelope")]
    public void Envelope_Decrypt()
    {
        if (SymmetricEngine.Decrypt(_key, _envelope, out var pt))
        {
            _sink ^= Fnv1a64(pt);
        }
        else
        {
            _sink ^= 0xDEADBEEFDEADBEEFul;
        }
    }

    // -------- Helpers --------

    private static System.Byte[] GenerateKeyFor(CipherSuiteType type) => type switch
    {
        CipherSuiteType.ChaCha20 => Bytes(32, 0x11112222u),
        CipherSuiteType.Salsa20 => Bytes(32, 0x22223333u), // Salsa20 supports 16/32; use 32
        CipherSuiteType.Speck => Bytes(Speck.KeySizeBytes, 0x33334444u),
        CipherSuiteType.Xtea => Bytes(16, 0x44445555u), // engine also accepts 32 then reduces; keep 16 for stability
        _ => throw new System.ArgumentOutOfRangeException(nameof(type))
    };

    private static System.Byte[] GenerateNonceFor(CipherSuiteType type) => type switch
    {
        CipherSuiteType.ChaCha20 => Bytes(ChaCha20.NonceSize, 0xAAAA0001u),
        CipherSuiteType.Salsa20 => Bytes(8, 0xAAAA0002u),
        CipherSuiteType.Speck => Bytes(16, 0xAAAA0003u),
        CipherSuiteType.Xtea => Bytes(8, 0xAAAA0004u),
        _ => throw new System.ArgumentOutOfRangeException(nameof(type))
    };

    private static System.Byte[] Bytes(System.Int32 len, System.UInt32 seed)
    {
        var b = new System.Byte[len];
        FillDeterministic(b, seed);
        return b;
    }

    private static System.Byte[] CreateDeterministicBytes(System.Int32 len, System.UInt32 seed)
    {
        var buf = new System.Byte[len];
        FillDeterministic(buf, seed);
        return buf;
    }

    /// <summary>Deterministic xorshift32 filler (not cryptographically secure).</summary>
    private static void FillDeterministic(System.Span<System.Byte> span, System.UInt32 seed)
    {
        System.UInt32 s = seed == 0 ? 1u : seed;
        for (System.Int32 i = 0; i < span.Length; i++)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            span[i] = (System.Byte)(s & 0xFF);
        }
    }

    /// <summary>64-bit FNV-1a to consume outputs.</summary>
    private static System.UInt64 Fnv1a64(System.ReadOnlySpan<System.Byte> data)
    {
        const System.UInt64 offset = 1469598103934665603ul;
        const System.UInt64 prime = 1099511628211ul;
        System.UInt64 hash = offset;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }
}
