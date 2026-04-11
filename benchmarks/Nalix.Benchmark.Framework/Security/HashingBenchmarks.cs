using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Security.Hashing;

namespace Nalix.Benchmark.Framework.Security;

/// <summary>
/// Benchmarks for Keccak256 and Poly1305 hashing performance.
/// </summary>
public class HashingBenchmarks : NalixBenchmarkBase
{
    private byte[] _data = null!;
    private byte[] _key = null!;
    private byte[] _tag = null!;
    private byte[] _hash = null!;

    [Params(64, 4096)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[PayloadBytes];
        _key = new byte[32];
        _tag = new byte[16];
        _hash = new byte[32];

        System.Random.Shared.NextBytes(_data);
        System.Random.Shared.NextBytes(_key);

        Poly1305.Compute(_key, _data, _tag);
    }

    [BenchmarkCategory("Keccak256"), Benchmark(Baseline = true)]
    public void Keccak256Hash() => Keccak256.HashData(_data, _hash);

    [BenchmarkCategory("Keccak256"), Benchmark]
    public bool Keccak256TryHash() => Keccak256.TryHashData(_data, _hash);

    [BenchmarkCategory("Poly1305"), Benchmark]
    public void Poly1305Compute() => Poly1305.Compute(_key, _data, _tag);

    [BenchmarkCategory("Poly1305"), Benchmark]
    public bool Poly1305Verify() => Poly1305.Verify(_key, _data, _tag);
}
