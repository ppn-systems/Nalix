using System;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.Memory.Buffers;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class DataWriterBenchmarks
{
    private byte[] _payload = null!;
    private byte[] _fixedBuffer = null!;

    [Params(128, 4096)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[this.PayloadBytes];
        _fixedBuffer = new byte[this.PayloadBytes * 2];

        for (int i = 0; i < _payload.Length; i++)
        {
            _payload[i] = (byte)(i % 199);
        }
    }

    [Benchmark]
    public int Write_WithRentedBuffer()
    {
        DataWriter writer = new(this.PayloadBytes);
        try
        {
            MemoryExtensions.AsSpan(_payload).CopyTo(writer.FreeBuffer);
            writer.Advance(_payload.Length);
            return writer.WrittenCount;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Benchmark]
    public int Expand_ThenWrite()
    {
        DataWriter writer = new(32);
        try
        {
            writer.Expand(this.PayloadBytes);
            MemoryExtensions.AsSpan(_payload).CopyTo(writer.FreeBuffer);
            writer.Advance(_payload.Length);
            return writer.WrittenCount;
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Benchmark]
    public int Write_WithFixedArray()
    {
        DataWriter writer = new(_fixedBuffer);
        try
        {
            MemoryExtensions.AsSpan(_payload).CopyTo(writer.FreeBuffer);
            writer.Advance(_payload.Length);
            return writer.WrittenCount;
        }
        finally
        {
            writer.Dispose();
        }
    }
}
