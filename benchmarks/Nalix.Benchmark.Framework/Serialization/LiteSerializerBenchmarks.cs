using BenchmarkDotNet.Attributes;
using Nalix.Framework.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class LiteSerializerBenchmarks
{
    private BenchPayload _payload = null!;
    private byte[] _payloadBytes = null!;
    private byte[] _intArrayBytes = null!;
    private byte[] _spanBuffer = null!;
    private int[] _intArray = null!;
    private BenchPoint _point;

    [Params(16, 128)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _intArray = new int[this.ItemCount];

        for (int i = 0; i < _intArray.Length; i++)
        {
            _intArray[i] = i * 17;
        }

        _payload = new BenchPayload
        {
            Id = 42,
            Name = $"payload-{this.ItemCount}",
            Values = _intArray,
            Enabled = true
        };

        _payloadBytes = LiteSerializer.Serialize(_payload);
        _intArrayBytes = LiteSerializer.Serialize(_intArray);
        _point = new BenchPoint(123, 456);
        _spanBuffer = new byte[LiteSerializer.Serialize(_point).Length];
    }

    [Benchmark]
    public byte[] Serialize_UnmanagedStruct()
        => LiteSerializer.Serialize(_point);

    [Benchmark]
    public int Serialize_UnmanagedStruct_IntoSpan()
        => LiteSerializer.Serialize(_point, _spanBuffer);

    [Benchmark]
    public byte[] Serialize_Object()
        => LiteSerializer.Serialize(_payload);

    [Benchmark]
    public byte[] Serialize_UnmanagedArray()
        => LiteSerializer.Serialize(_intArray);

    [Benchmark]
    public BenchPayload Deserialize_Object()
        => LiteSerializer.Deserialize<BenchPayload>(_payloadBytes, out _)!;

    [Benchmark]
    public int[] Deserialize_UnmanagedArray()
        => LiteSerializer.Deserialize<int[]>(_intArrayBytes, out _)!;

    public readonly record struct BenchPoint(int X, int Y);

    public sealed class BenchPayload
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int[] Values { get; set; } = [];

        public bool Enabled { get; set; }
    }
}
