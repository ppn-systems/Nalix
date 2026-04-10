using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Nalix.Framework.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

[MemoryDiagnoser]
[DisassemblyDiagnoser(printSource: true)]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
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
