// Copyright (c) 2026 PPN Corporation. All rights reserved.

//  13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
//  .NET SDK 10.0.103
//  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3 [AttachedDebugger]
//  DefaultJob : .NET 10.0.3(10.0.3, 10.0.326.7603), X64 RyuJIT x86 - 64 - v3

//| Method                                                | KeyPairCount | Mean     | Error    | StdDev   | Allocated |
//|------------------------------------------------------ |------------- |---------:|---------:|---------:|----------:|
//| 'X25519.GenerateKeyPair (CSPRNG + scalar mult)'       | 1            | 65.36 us | 1.058 us | 0.989 us |     112 B |
//| 'X25519.GenerateKeyFromPrivateKey (scalar mult only)' | 1            | 67.35 us | 1.294 us | 1.147 us |     112 B |
//| 'X25519.Agreement (shared secret)'                    | 1            | 66.59 us | 1.321 us | 1.622 us |      56 B |
//| 'X25519.GenerateKeyPair (CSPRNG + scalar mult)'       | 4            | 65.76 us | 1.310 us | 1.609 us |     112 B |
//| 'X25519.GenerateKeyFromPrivateKey (scalar mult only)' | 4            | 65.96 us | 1.301 us | 1.336 us |     112 B |
//| 'X25519.Agreement (shared secret)'                    | 4            | 65.95 us | 1.297 us | 1.543 us |      56 B |
//| 'X25519.GenerateKeyPair (CSPRNG + scalar mult)'       | 16           | 67.09 us | 1.329 us | 1.422 us |     112 B |
//| 'X25519.GenerateKeyFromPrivateKey (scalar mult only)' | 16           | 66.36 us | 1.276 us | 1.566 us |     112 B |
//| 'X25519.Agreement (shared secret)'                    | 16           | 66.34 us | 1.309 us | 1.747 us |      56 B |

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using Nalix.Shared.Security.Asymmetric; // X25519
using System;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Shared.Security.Asymmetric;

// Measure allocations and time for keygen and agreement operations.
[RankColumn]
[MemoryDiagnoser]
[DisassemblyDiagnoser]
[HardwareCounters(
    HardwareCounter.BranchInstructions,
    HardwareCounter.BranchMispredictions,
    HardwareCounter.CacheMisses,
    HardwareCounter.InstructionRetired)]
[MinColumn, MaxColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
public class X25519Benchmarks
{
    // Number of pre-generated keypairs used for Agreement benchmark permutations.
    [Params(1, 4, 16)]
    public Int32 KeyPairCount;

    private Byte[] _privateKeySample = Array.Empty<Byte>();
    private X25519.X25519KeyPair[] _keyPairs = Array.Empty<X25519.X25519KeyPair>();

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Prepare a deterministic random private key for public-key derivation benchmark
        _privateKeySample = new Byte[32];
        RandomNumberGenerator.Fill(_privateKeySample);

        // Prepare several keypairs for Agreement benchmark
        _keyPairs = new X25519.X25519KeyPair[KeyPairCount];
        for (Int32 i = 0; i < KeyPairCount; i++)
        {
            _keyPairs[i] = X25519.GenerateKeyPair();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        // Clear sensitive material
        if (_privateKeySample != null)
        {
            Array.Clear(_privateKeySample, 0, _privateKeySample.Length);
        }

        if (_keyPairs != null)
        {
            for (Int32 i = 0; i < _keyPairs.Length; i++)
            {
                if (_keyPairs[i].PrivateKey != null)
                {
                    Array.Clear(_keyPairs[i].PrivateKey, 0, _keyPairs[i].PrivateKey.Length);
                }

                if (_key_pairs_has_public(_keyPairs[i]))
                {
                    Array.Clear(_keyPairs[i].PublicKey, 0, _keyPairs[i].PublicKey.Length);
                }
            }
        }

        static Boolean _key_pairs_has_public(X25519.X25519KeyPair kp) => kp.PublicKey != null;
    }

    // Benchmark: Generate new keypair (includes CSPRNG + public derivation)
    [Benchmark(Description = "X25519.GenerateKeyPair (CSPRNG + scalar mult)")]
    public X25519.X25519KeyPair GenerateKeyPair() => X25519.GenerateKeyPair();

    // Benchmark: Derive public key from an existing private key (no RNG)
    [Benchmark(Description = "X25519.GenerateKeyFromPrivateKey (scalar mult only)")]
    public X25519.X25519KeyPair GenerateFromPrivate() => X25519.GenerateKeyFromPrivateKey((Byte[])_privateKeySample.Clone());

    // Benchmark: Agreement between two generated keypairs (shared secret)
    // Use round-robin pairs from pre-generated array to avoid measuring keygen cost here.
    [Benchmark(Description = "X25519.Agreement (shared secret)")]
    public Byte[] Agreement()
    {
        // Pair index rotates to exercise different inputs
        Int32 i = (Environment.TickCount & 0x7FFFFFFF) % KeyPairCount;
        Int32 j = (i + 1) % KeyPairCount;

        var a = _key_pairs_get(_keyPairs, i);
        var b = _key_pairs_get(_keyPairs, j);

        // Agreement returns a new byte[] (shared secret)
        return X25519.Agreement(a.PrivateKey, b.PublicKey);

        static X25519.X25519KeyPair _key_pairs_get(X25519.X25519KeyPair[] arr, Int32 idx) => arr[idx];
    }
}