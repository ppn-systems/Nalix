using BenchmarkDotNet.Attributes;
using Nalix.Common.Abstractions;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Benchmark.Framework.Memory.Objects;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class ObjectPoolManagerBenchmarks
{
    private ObjectPoolManager _manager = null!;
    private TypedObjectPoolAdapter<PooledEntry> _typedPool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _manager = new ObjectPoolManager();
        _ = _manager.Prealloc<PooledEntry>(32);
        _typedPool = _manager.GetTypedPool<PooledEntry>();
    }

    [Benchmark]
    public PooledEntry Get_Return()
    {
        PooledEntry value = _manager.Get<PooledEntry>();
        _manager.Return(value);
        return value;
    }

    [Benchmark]
    public PooledEntry TypedPool_Get_Return()
    {
        PooledEntry value = _typedPool.Get();
        _typedPool.Return(value);
        return value;
    }

    [Benchmark]
    public int PerformHealthCheck()
        => _manager.PerformHealthCheck();

    public sealed class PooledEntry : IPoolable
    {
        public int Id;

        public void ResetForPool() => Id = 0;
    }
}
