using BenchmarkDotNet.Attributes;
using Nalix.Framework.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class ArrayBenchmarks
{
    private int[] _array = null!;
    private byte[] _bytes = null!;

    [Params(16, 128, 1024)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _array = new int[this.ItemCount];
        for (int i = 0; i < _array.Length; i++)
        {
            _array[i] = i * 17;
        }

        _bytes = LiteSerializer.Serialize(_array);
    }

    [Benchmark(Baseline = true)]
    public byte[] Serialize_Array()
        => LiteSerializer.Serialize(_array);

    [Benchmark]
    public int[] Deserialize_Array()
        => LiteSerializer.Deserialize<int[]>(_bytes, out _)!;
}
