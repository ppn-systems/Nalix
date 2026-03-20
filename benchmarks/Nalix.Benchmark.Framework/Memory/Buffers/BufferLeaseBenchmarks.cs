using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;

namespace Nalix.Benchmark.Framework.Memory.Buffers;

/// <summary>
/// Benchmarks for BufferLease lifecycle: Rent, CopyFrom, Retain, and pool-return paths.
/// MemoryDiagnoser shows Gen0 and Allocated to verify zero-allocation on the managed pool path.
/// </summary>
[MemoryDiagnoser]
public class BufferLeaseBenchmarks : NalixBenchmarkBase
{
    private byte[] _source = null!;
    private BufferPoolManager _manager = null!;

    [Params(256, 2048)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = new byte[this.PayloadBytes];
        System.Random.Shared.NextBytes(_source);

        // Wire BufferLease.ByteArrayPool → managed slab pool so the return path is exercised.
        _manager = new BufferPoolManager(new BufferOptions
        {
            EnableMemoryTrimming = false,
            EnableAnalytics = false,
            FallbackToArrayPool = true,
            TotalBuffers = 2048
        });
        BufferLease.ByteArrayPool.Configure(_manager);
    }

    [GlobalCleanup]
    public void Cleanup() => _manager.Dispose();

    /// <summary>Hot path: rent → write → commit → dispose. Verifies slab-aware Return.</summary>
    [Benchmark(Baseline = true)]
    public int RentCommitAndDispose()
    {
        using BufferLease lease = BufferLease.Rent(this.PayloadBytes);
        _source.AsSpan().CopyTo(lease.SpanFull);
        lease.CommitLength(_source.Length);
        return lease.Length;
    }

    /// <summary>CopyFrom path: allocates + copies + returns via slab segment.</summary>
    [Benchmark]
    public int CopyFromAndDispose()
    {
        using BufferLease lease = BufferLease.CopyFrom(_source);
        return lease.Length;
    }

    /// <summary>Retain + double-owner handoff: exercises the ref-count atomic path.</summary>
    [Benchmark]
    public int RentRetainDispose()
    {
        using BufferLease lease = BufferLease.Rent(this.PayloadBytes);
        lease.Retain();          // refCount = 2
        lease.CommitLength(this.PayloadBytes);
        lease.Dispose();         // refCount = 1 (no return yet)
        return lease.Length;     // still valid here
        // Outer using: refCount → 0 → Return()
    }

    /// <summary>Raw manager Rent+Return for direct comparison baseline.</summary>
    [Benchmark]
    public byte[] ManagerRentAndReturn()
    {
        byte[] buf = _manager.Rent(this.PayloadBytes);
        _manager.Return(buf);
        return buf;
    }
}
