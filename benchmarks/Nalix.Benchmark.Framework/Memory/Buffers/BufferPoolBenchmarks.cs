using System;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.Memory.Buffers;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class BufferPoolBenchmarks
{
    private BufferPoolManager _manager = null!;

    [Params(256, 4096)]
    public int BufferSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _manager = new BufferPoolManager(new BufferConfig
        {
            EnableMemoryTrimming = false,
            EnableAnalytics = false,
            FallbackToArrayPool = true,
            TotalBuffers = 128
        });
    }

    [GlobalCleanup]
    public void Cleanup() => _manager.Dispose();

    [Benchmark]
    public byte[] Rent_Return_Array()
    {
        byte[] buffer = _manager.Rent(this.BufferSize);
        _manager.Return(buffer);
        return buffer;
    }

    [Benchmark]
    public ArraySegment<byte> Rent_Return_Segment()
    {
        ArraySegment<byte> segment = _manager.RentSegment(this.BufferSize);
        _manager.Return(segment);
        return segment;
    }

    [Benchmark]
    public double GetAllocationForSize()
        => _manager.GetAllocationForSize(this.BufferSize);
}
