using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.Memory.Buffers;

/// <summary>
/// Benchmarks for BufferLease lifecycle and data operations.
/// </summary>
public class BufferLeaseBenchmarks : NalixBenchmarkBase
{
    private byte[] _source = null!;

    [Params(128, 2048)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = new byte[PayloadBytes];
        Random.Shared.NextBytes(_source);
    }

    [Benchmark]
    public int RentCommitAndDispose()
    {
        using BufferLease lease = BufferLease.Rent(PayloadBytes);
        _source.AsSpan().CopyTo(lease.SpanFull);
        lease.CommitLength(_source.Length);
        return lease.Length;
    }

    [Benchmark]
    public int CopyFromAndDispose()
    {
        using BufferLease lease = BufferLease.CopyFrom(_source);
        return lease.Length;
    }
}
