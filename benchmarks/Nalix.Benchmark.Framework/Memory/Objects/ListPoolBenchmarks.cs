using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Memory.Pools;

namespace Nalix.Benchmark.Framework.Memory.Objects;

/// <summary>
/// Benchmarks for ListPool performance including renting, filling, and returning lists.
/// </summary>
public class ListPoolBenchmarks : NalixBenchmarkBase
{
    private ListPool<int> _pool = null!;

    [Params(32, 256)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pool = new ListPool<int>(256, 16);
        _pool.Prealloc(32, ItemCount);
    }

    [Benchmark]
    public List<int> RentFillAndReturn()
    {
        List<int> list = _pool.Rent(ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            list.Add(i);
        }

        _pool.Return(list);
        return list;
    }

    [Benchmark]
    public int TrimPool() => _pool.Trim(16);
}
