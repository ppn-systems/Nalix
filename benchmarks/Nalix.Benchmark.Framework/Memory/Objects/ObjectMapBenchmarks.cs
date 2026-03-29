using BenchmarkDotNet.Attributes;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Benchmark.Framework.Memory.Objects;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class ObjectMapBenchmarks
{
    [Params(32, 256)]
    public int ItemCount { get; set; }

    [Benchmark]
    public int Rent_Add_Read_Return()
    {
        ObjectMap<int, string> map = ObjectMap<int, string>.Rent();
        try
        {
            for (int i = 0; i < this.ItemCount; i++)
            {
                map.Add(i, $"value-{i}");
            }

            _ = map.TryGetValue(this.ItemCount / 2, out _);
            return map.Count;
        }
        finally
        {
            map.Return();
        }
    }
}
