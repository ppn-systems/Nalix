using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Nalix.Framework.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

[DisassemblyDiagnoser(printSource: true)]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class StructBenchmarks
{
    private ComplexStruct _payload;
    private byte[] _buffer = null!;
    private readonly Consumer _consumer = new();

    [GlobalSetup]
    public void Setup()
    {
        _payload = new ComplexStruct(
            Id: 1000,
            Name: "Nalix Core Engine",
            Flags: [1, 2, 3, 4, 5, 6, 7, 8],
            Location: new BenchPoint(1, 2, 3, 4)
        );

        // Chuẩn bị sẵn buffer thừa thãi cho Variable-Length Allocation
        _buffer = new byte[1024];
    }

    [Benchmark(Baseline = true)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Serialize()
        => _consumer.Consume(LiteSerializer.Serialize(_payload));

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Serialize_IntoSpan()
        => _consumer.Consume(LiteSerializer.Serialize(_payload, _buffer));

    // Mẫu Struct Unmanaged
    public readonly record struct BenchPoint(long A, long B, long C, long D);

    // Mẫu Struct Thực tế chứa cả Value Type, Reference Type, và Nested Formatter
    public struct ComplexStruct
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int[] Flags { get; set; }
        public BenchPoint Location { get; set; }

        public ComplexStruct(int Id, string Name, int[] Flags, BenchPoint Location)
        {
            this.Id = Id;
            this.Name = Name;
            this.Flags = Flags;
            this.Location = Location;
        }
    }
}
