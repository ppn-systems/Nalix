using System;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.Memory.Buffers;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class BufferLeaseBenchmarks
{
    private byte[] _source = null!;

    [Params(128, 2048)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = new byte[this.PayloadBytes];
        for (int i = 0; i < _source.Length; i++)
        {
            _source[i] = (byte)(i % 251);
        }
    }

    [Benchmark]
    public int Rent_Commit_Dispose()
    {
        using BufferLease lease = BufferLease.Rent(this.PayloadBytes);
        MemoryExtensions.AsSpan(_source).CopyTo(lease.SpanFull);
        lease.CommitLength(_source.Length);
        return lease.Length;
    }

    [Benchmark]
    public int CopyFrom_Dispose()
    {
        using BufferLease lease = BufferLease.CopyFrom(_source);
        return lease.Length;
    }
}
