using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmarks.Shared;
using Nalix.Benchmarks.Shared.Payloads;
using Nalix.Codec.Serialization;

namespace Nalix.Codec.Benchmarks.Serialization;

[Config(typeof(NalixBenchmarkConfig))]
public class LiteSerializerBenchmarks
{
    private SmallStruct _smallStruct;
    private LargeStruct _largeStruct;
    private BenchPayload _payload = null!;

    private byte[] _smallBytes = null!;
    private byte[] _largeBytes = null!;
    private byte[] _payloadBytes = null!;

    private byte[] _targetBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallStruct = new SmallStruct { Field1 = 1, Field2 = 2, Field3 = 3, Field4 = 4 };
        _largeStruct = new LargeStruct { FirstField = 100, LastField = 200 };
        
        _payload = new BenchPayload
        {
            Id = 99,
            Name = "A moderately sized string for custom formatter benchmarking purposes.",
            Items = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100]
        };

        // Pre-serialize
        _smallBytes = LiteSerializer.Serialize(_smallStruct);
        _largeBytes = LiteSerializer.Serialize(_largeStruct);
        _payloadBytes = LiteSerializer.Serialize(_payload);

        _targetBuffer = new byte[1024];
    }

    [Benchmark]
    public byte[] Serialize_Unmanaged_Small() => LiteSerializer.Serialize(_smallStruct);

    [Benchmark]
    public byte[] Serialize_Unmanaged_Large() => LiteSerializer.Serialize(_largeStruct);

    [Benchmark]
    public SmallStruct Deserialize_Unmanaged_Small()
    {
        SmallStruct result = default;
        LiteSerializer.Deserialize(_smallBytes, ref result);
        return result;
    }

    [Benchmark]
    public LargeStruct Deserialize_Unmanaged_Large()
    {
        LargeStruct result = default;
        LiteSerializer.Deserialize(_largeBytes, ref result);
        return result;
    }

    [Benchmark]
    public byte[] Serialize_Formatter() => LiteSerializer.Serialize(_payload);

    [Benchmark]
    public BenchPayload Deserialize_Formatter()
    {
        BenchPayload? result = null;
        LiteSerializer.Deserialize(_payloadBytes, ref result);
        return result!;
    }

    [Benchmark]
    public int Fill_IntoSpan_Small()
    {
        return LiteSerializer.Serialize(_smallStruct, _targetBuffer.AsSpan());
    }

    [Benchmark]
    public int Fill_IntoSpan_Large()
    {
        return LiteSerializer.Serialize(_largeStruct, _targetBuffer.AsSpan());
    }

    [Benchmark]
    public object Resolve_Formatter()
    {
        return FormatterProvider.Get<BenchPayload>();
    }
}
