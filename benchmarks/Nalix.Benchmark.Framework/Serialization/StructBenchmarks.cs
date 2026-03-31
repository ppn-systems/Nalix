using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Nalix.Framework.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 15)]
[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
[DisassemblyDiagnoser(printSource: true)]
public class StructBenchmarks
{
    private BenchPoint _point;
    private byte[] _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _point = new BenchPoint(123, 456);

        // ✅ FIX: không gọi Serialize ở đây
        _buffer = new byte[Unsafe.SizeOf<BenchPoint>()];
    }

    [Benchmark(Baseline = true)]
    public byte[] Serialize()
        => LiteSerializer.Serialize(_point);

    [Benchmark]
    public int Serialize_IntoSpan()
        => LiteSerializer.Serialize(_point, _buffer);

    public readonly record struct BenchPoint(int X, int Y);
}
