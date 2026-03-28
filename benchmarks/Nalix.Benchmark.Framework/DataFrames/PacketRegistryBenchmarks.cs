using BenchmarkDotNet.Attributes;
using Nalix.Framework.DataFrames;

namespace Nalix.Benchmark.Framework.DataFrames;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class PacketRegistryBenchmarks
{
    [Benchmark]
    public PacketRegistry CreateDefaultCatalog()
        => new PacketRegistryFactory().CreateCatalog();
}
