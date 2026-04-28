using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Environment.Random;

namespace Nalix.Benchmark.Framework.Random;

/// <summary>
/// Benchmarks for Cryptographically Secure Pseudo-Random Number Generator (CSPRNG) performance.
/// </summary>
public class RandomBenchmarks : NalixBenchmarkBase
{
    private byte[] _buffer = null!;

    [Params(32, 1024)]
    public int ByteLength { get; set; }

    [GlobalSetup]
    public void Setup() => _buffer = new byte[ByteLength];

    [Benchmark]
    public void FillBuffer() => Csprng.Fill(_buffer);

    [Benchmark]
    public byte[] GenerateBytes() => Csprng.GetBytes(ByteLength);

    [Benchmark]
    public byte[] GenerateNonce() => Csprng.CreateNonce(12);

    [Benchmark]
    public ulong NextUInt64() => Csprng.NextUInt64();
}