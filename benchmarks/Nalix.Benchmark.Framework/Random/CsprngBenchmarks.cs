using BenchmarkDotNet.Attributes;
using Nalix.Framework.Random;

namespace Nalix.Benchmark.Framework.Random;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class CsprngBenchmarks
{
    private byte[] _buffer = null!;

    [Params(32, 1024)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup() => _buffer = new byte[this.Length];

    [Benchmark]
    public void Fill()
        => Csprng.Fill(_buffer);

    [Benchmark]
    public byte[] GetBytes()
        => Csprng.GetBytes(this.Length);

    [Benchmark]
    public byte[] CreateNonce()
        => Csprng.CreateNonce(12);

    [Benchmark]
    public ulong NextUInt64()
        => Csprng.NextUInt64();
}
