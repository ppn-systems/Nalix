using System.Text.Json;
using BenchmarkDotNet.Attributes;
using MessagePack;
using MessagePack.Resolvers;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Serialization;

namespace Nalix.Benchmark.Framework.Serialization;

/// <summary>
/// Compares LiteSerializer against System.Text.Json and MessagePack
/// for the same payload shape.
/// </summary>
public class SerializerComparisonBenchmarks : NalixBenchmarkBase
{
    private static readonly MessagePackSerializerOptions MessagePackOptions =
        MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

    private static readonly JsonSerializerOptions JsonOptions = new();

    private BenchPayload _payload = null!;
    private byte[] _liteBytes = null!;
    private byte[] _jsonBytes = null!;
    private byte[] _messagePackBytes = null!;

    [Params(16, 128, 1024)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        int[] values = new int[ItemCount];
        for (int i = 0; i < values.Length; i++)
            values[i] = i;

        _payload = new BenchPayload
        {
            Id = 42,
            Name = $"Payload-{ItemCount}",
            Values = values,
            Enabled = true,
            Nested = new NestedPayload
            {
                Seed = 987654321L,
                Tags = ["alpha", "beta", "gamma"]
            }
        };

        _liteBytes = LiteSerializer.Serialize(_payload);
        _jsonBytes = JsonSerializer.SerializeToUtf8Bytes(_payload, JsonOptions);
        _messagePackBytes = MessagePackSerializer.Serialize(_payload, MessagePackOptions);
    }

    [Benchmark(Baseline = true, Description = "LiteSerializer Serialize")]
    public byte[] LiteSerializerSerialize() => LiteSerializer.Serialize(_payload);

    [Benchmark(Description = "LiteSerializer Deserialize")]
    public BenchPayload LiteSerializerDeserialize() => LiteSerializer.Deserialize<BenchPayload>(_liteBytes, out _)!;

    [Benchmark(Description = "System.Text.Json Serialize")]
    public byte[] SystemTextJsonSerialize() => JsonSerializer.SerializeToUtf8Bytes(_payload, JsonOptions);

    [Benchmark(Description = "System.Text.Json Deserialize")]
    public BenchPayload SystemTextJsonDeserialize() => JsonSerializer.Deserialize<BenchPayload>(_jsonBytes, JsonOptions)!;

    [Benchmark(Description = "MessagePack Serialize")]
    public byte[] MessagePackSerialize() => MessagePackSerializer.Serialize(_payload, MessagePackOptions);

    [Benchmark(Description = "MessagePack Deserialize")]
    public BenchPayload MessagePackDeserialize() => MessagePackSerializer.Deserialize<BenchPayload>(_messagePackBytes, MessagePackOptions)!;

    public sealed class BenchPayload
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] Values { get; set; } = [];
        public bool Enabled { get; set; }
        public NestedPayload Nested { get; set; } = new();
    }

    public sealed class NestedPayload
    {
        public long Seed { get; set; }
        public string[] Tags { get; set; } = [];
    }
}
