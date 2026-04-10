using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Nalix.Framework.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class ObjectBenchmarks
{
    private BenchPayload _payload = null!;
    private byte[] _bytes = null!;

    [Params(16, 128)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        int[] array = new int[this.ItemCount];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = i * 17;
        }

        _payload = new BenchPayload
        {
            Id = 42,
            Name = $"payload-{this.ItemCount}",
            Values = array,
            Enabled = true
        };

        _bytes = LiteSerializer.Serialize(_payload);
    }

    [Benchmark(Baseline = true)]
    public byte[] Serialize_Object()
        => LiteSerializer.Serialize(_payload);

    [Benchmark]
    public BenchPayload Deserialize_Object()
        => LiteSerializer.Deserialize<BenchPayload>(_bytes, out _)!;

    public sealed class BenchPayload
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] Values { get; set; } = [];
        public bool Enabled { get; set; }
    }
}
