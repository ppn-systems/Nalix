using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Nalix.Benchmark.Framework.Abstractions;

/// <summary>
/// Base class for all Nalix benchmarks, providing standard configuration.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public abstract class NalixBenchmarkBase
{
}
