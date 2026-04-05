using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;

namespace Nalix.Benchmark.Framework.Memory.Buffers;

/// <summary>
/// Benchmarks for BufferPoolManager performance.
/// </summary>
public class BufferPoolBenchmarks : NalixBenchmarkBase
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
            TotalBuffers = 1024
        });
    }

    [GlobalCleanup]
    public void Cleanup() => _manager.Dispose();

    [Benchmark]
    public byte[] RentAndReturnArray()
    {
        byte[] buffer = _manager.Rent(BufferSize);
        _manager.Return(buffer);
        return buffer;
    }


    [Benchmark]
    public double QueryAllocationRate() => _manager.GetAllocationForSize(BufferSize);
}
