using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Codec.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

/// <summary>
/// Benchmarks for LiteSerializer performance across different data types including objects, structs, and arrays.
/// Tests both serialization and deserialization overhead.
/// </summary>
public class LiteSerializerBenchmarks : NalixBenchmarkBase
{
    private BenchPayload _objectPayload = null!;
    private ComplexStruct _structPayload;
    private int[] _arrayPayload = null!;
    
    private byte[] _objectBytes = null!;
    private byte[] _structBytes = null!;
    private byte[] _arrayBytes = null!;

    [Params(16, 128)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Array Setup
        _arrayPayload = new int[ItemCount];
        for (int i = 0; i < _arrayPayload.Length; i++) _arrayPayload[i] = i;
        _arrayBytes = LiteSerializer.Serialize(_arrayPayload);

        // Object Setup
        _objectPayload = new BenchPayload
        {
            Id = 42,
            Name = $"Payload-{ItemCount}",
            Values = _arrayPayload,
            Enabled = true
        };
        _objectBytes = LiteSerializer.Serialize(_objectPayload);

        // Struct Setup
        _structPayload = new ComplexStruct(
            id: 1000,
            name: "Nalix Core",
            flags: _arrayPayload,
            location: new BenchPoint(1, 2, 3, 4)
        );
        _structBytes = LiteSerializer.Serialize(_structPayload);
    }

    /// <summary>Serializes a standard POCO object.</summary>
    [BenchmarkCategory("Serialization"), Benchmark(Baseline = true, Description = "Serialize (Object)")]
    public byte[] SerializeObject() => LiteSerializer.Serialize(_objectPayload);

    /// <summary>Deserializes a standard POCO object.</summary>
    [BenchmarkCategory("Deserialization"), Benchmark(Description = "Deserialize (Object)")]
    public BenchPayload DeserializeObject() => LiteSerializer.Deserialize<BenchPayload>(_objectBytes, out _)!;

    /// <summary>Serializes a complex value-type struct.</summary>
    [BenchmarkCategory("Serialization"), Benchmark(Description = "Serialize (Struct)")]
    public byte[] SerializeStruct() => LiteSerializer.Serialize(_structPayload);

    /// <summary>Deserializes a complex value-type struct.</summary>
    [BenchmarkCategory("Deserialization"), Benchmark(Description = "Deserialize (Struct)")]
    public ComplexStruct DeserializeStruct() => LiteSerializer.Deserialize<ComplexStruct>(_structBytes, out _);

    /// <summary>Serializes a primitive array.</summary>
    [BenchmarkCategory("Serialization"), Benchmark(Description = "Serialize (Array)")]
    public byte[] SerializeArray() => LiteSerializer.Serialize(_arrayPayload);

    /// <summary>Deserializes a primitive array.</summary>
    [BenchmarkCategory("Deserialization"), Benchmark(Description = "Deserialize (Array)")]
    public int[] DeserializeArray() => LiteSerializer.Deserialize<int[]>(_arrayBytes, out _)!;

    // --- Data Types ---
    
    public sealed class BenchPayload
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] Values { get; set; } = [];
        public bool Enabled { get; set; }
    }

    public readonly record struct BenchPoint(long A, long B, long C, long D);

    public struct ComplexStruct(int id, string name, int[] flags, BenchPoint location)
    {
        public int Id { get; set; } = id;
        public string Name { get; set; } = name;
        public int[] Flags { get; set; } = flags;
        public BenchPoint Location { get; set; } = location;
    }
}