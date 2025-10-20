// Copyright (c) 2025 PPN Corporation. All rights reserved.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using Nalix.Common.Enums;
using Nalix.Framework.Cryptography.Aead;

namespace Nalix.Framework.Benchmark;

/// <summary>
/// BenchmarkDotNet suite for AeadEngine Encrypt/Decrypt throughput.
/// Measures across algorithms and payload sizes,
/// preallocating inputs to reduce measurement noise.
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[ThreadingDiagnoser]
[HideColumns(["StdErr", "Median", "RatioSD"])]
[Orderer(SummaryOrderPolicy.Declared)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
[CategoriesColumn]
[Config(typeof(BenchmarkConfig))]
public class AeadEngineBenchmarks
{
    // -------- Parameters --------

    [Params(
        CipherSuiteType.ChaCha20Poly1305,
        CipherSuiteType.Salsa20Poly1305,
        CipherSuiteType.SpeckPoly1305,
        CipherSuiteType.XteaPoly1305)]
    public CipherSuiteType Algorithm { get; set; }

    [Params(0, 64, 1024, 65536, 1048576)]
    public System.Int32 PayloadSize { get; set; }

    // -------- State --------

    private System.Byte[] _key = default!;
    private System.Byte[] _aad = default!;
    private System.Byte[] _plaintext = default!;
    private System.Byte[] _envelope = default!;

    // A rolling checksum to prevent dead-code elimination.
    private System.UInt64 _sink;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Prepare deterministic data for stability (not cryptographic RNG).
        // You can switch to SecureRandom.Fill if you prefer full randomness.
        _aad = CreateDeterministicBytes(32, seed: 0xAABD_1122u);
        _plaintext = CreateDeterministicBytes(PayloadSize, seed: 0xCCDD_EEFFu);

        _key = GenerateKeyFor(Algorithm);

        // Pre-encrypt once for Decrypt benchmarks (uses the same AAD convention as engine)
        _envelope = AeadEngine.Encrypt(_key, _plaintext, Algorithm, _aad, seq: 0x11223344u);
        Touch(_envelope);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        System.Array.Clear(_key, 0, _key.Length);
        System.Array.Clear(_aad, 0, _aad.Length);
        System.Array.Clear(_plaintext, 0, _plaintext.Length);
        System.Array.Clear(_envelope, 0, _envelope.Length);
    }

    // -------- Benchmarks --------

    [Benchmark(Description = "Encrypt", Baseline = true)]
    [BenchmarkCategory("Encrypt")]
    public void Encrypt()
    {
        var env = AeadEngine.Encrypt(_key, _plaintext, Algorithm, _aad, seq: 0x55667788u);
        _sink ^= Fnv1a64(env);
    }

    [Benchmark(Description = "Decrypt")]
    [BenchmarkCategory("Decrypt")]
    public void Decrypt()
    {
        if (AeadEngine.Decrypt(_key, _envelope, out var pt, _aad))
        {
            _sink ^= Fnv1a64(pt);
        }
        else
        {
            // Keep the branch to reflect potential failure paths (should not happen).
            _sink ^= 0xDEADBEEFDEADBEEFul;
        }
    }

    // -------- Helpers --------

    /// <summary>
    /// Generates algorithm-appropriate key length.
    /// </summary>
    private static System.Byte[] GenerateKeyFor(CipherSuiteType type)
    {
        System.Int32 len = type switch
        {
            CipherSuiteType.ChaCha20Poly1305 => 32,
            CipherSuiteType.Salsa20Poly1305 => 32, // pick 32 (Salsa20 supports 16 or 32)
            CipherSuiteType.SpeckPoly1305 => Cryptography.Symmetric.Suite.Speck.KeySizeBytes,
            CipherSuiteType.XteaPoly1305 => 16, // engine allows 16 or 32 (32 reduced), use 16 for stability
            _ => throw new System.ArgumentOutOfRangeException(nameof(type))
        };

        var key = new System.Byte[len];
        // Deterministic fill for stability; switch to SecureRandom.Fill(key) if desired.
        FillDeterministic(key, 0x1234_5678u + (System.UInt32)len);
        return key;
    }

    private static System.Byte[] CreateDeterministicBytes(System.Int32 len, System.UInt32 seed)
    {
        var buf = new System.Byte[len];
        FillDeterministic(buf, seed);
        return buf;
    }

    /// <summary>
    /// Simple xorshift32-based filler for reproducible inputs (not cryptographically secure).
    /// </summary>
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

    /// <summary>
    /// 64-bit FNV-1a hash to consume results.
    /// </summary>
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

    private void Touch(System.ReadOnlySpan<System.Byte> data)
    {
        _sink ^= Fnv1a64(data);
    }
}
