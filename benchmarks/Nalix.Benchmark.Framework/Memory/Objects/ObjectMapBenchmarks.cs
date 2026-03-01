using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Benchmark.Framework.Memory.Objects;

/// <summary>
/// Benchmarks for ObjectMap performance including renting, adding, and looking up items.
/// </summary>
public class ObjectMapBenchmarks : NalixBenchmarkBase
{
    [Params(32, 256)]
    public int ItemCount { get; set; }

    [Benchmark]
    public int RentAddAndLookup()
    {
        ObjectMap<int, string> map = ObjectMap<int, string>.Rent();
        try
        {
            for (int i = 0; i < ItemCount; i++)
            {
                map.Add(i, $"value-{i}");
            }

            _ = map.TryGetValue(ItemCount / 2, out _);
            return map.Count;
        }
        finally
        {
            map.Return();
        }
    }
}
