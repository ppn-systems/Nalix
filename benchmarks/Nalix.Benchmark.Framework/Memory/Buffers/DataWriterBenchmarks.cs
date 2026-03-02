using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.Memory.Buffers;

/// <summary>
/// Benchmarks for DataWriter performance and expansion strategies.
/// </summary>
public class DataWriterBenchmarks : NalixBenchmarkBase
{
    private byte[] _payload = null!;
    private byte[] _fixedBuffer = null!;

    [Params(128, 4096)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[PayloadBytes];
        _fixedBuffer = new byte[PayloadBytes * 2];
        System.Random.Shared.NextBytes(_payload);
    }

    [Benchmark]
    public int WriteWithRentedBuffer()
    {
        using DataWriter writer = new(PayloadBytes);
        _payload.AsSpan().CopyTo(writer.FreeBuffer);
        writer.Advance(_payload.Length);
        return writer.WrittenCount;
    }

    [Benchmark]
    public int ExpandThenWrite()
    {
        using DataWriter writer = new(32);
        writer.Expand(PayloadBytes);
        _payload.AsSpan().CopyTo(writer.FreeBuffer);
        writer.Advance(_payload.Length);
        return writer.WrittenCount;
    }

    [Benchmark]
    public int WriteWithFixedArray()
    {
        using DataWriter writer = new(_fixedBuffer);
        _payload.AsSpan().CopyTo(writer.FreeBuffer);
        writer.Advance(_payload.Length);
        return writer.WrittenCount;
    }
}
