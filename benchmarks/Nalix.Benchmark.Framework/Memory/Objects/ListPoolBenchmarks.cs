using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.Memory.Pools;

namespace Nalix.Benchmark.Framework.Memory.Objects;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class ListPoolBenchmarks
{
    private ListPool<int> _pool = null!;

    [Params(32, 256)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pool = new ListPool<int>(256, 16);
        _pool.Prealloc(32, this.ItemCount);
    }

    [Benchmark]
    public List<int> Rent_Fill_Return()
    {
        List<int> list = _pool.Rent(this.ItemCount);
        for (int i = 0; i < this.ItemCount; i++)
        {
            list.Add(i);
        }

        _pool.Return(list);
        return list;
    }

    [Benchmark]
    public int Trim()
        => _pool.Trim(16);
}
