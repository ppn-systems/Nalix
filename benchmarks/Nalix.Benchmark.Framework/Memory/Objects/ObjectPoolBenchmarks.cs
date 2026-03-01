using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Common.Abstractions;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Memory.Pools;

namespace Nalix.Benchmark.Framework.Memory.Objects;

/// <summary>
/// Benchmarks for ObjectPool and ObjectPoolManager performance.
/// </summary>
public class ObjectPoolBenchmarks : NalixBenchmarkBase
{
    private ObjectPool _pool = null!;
    private ObjectPoolManager _manager = null!;
    private TypedObjectPool<PooledItem> _poolTyped = null!;
    private TypedObjectPoolAdapter<PooledItem> _managerTyped = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new ObjectPool(256);
        _poolTyped = _pool.CreateTypedPool<PooledItem>();
        _pool.Prealloc<PooledItem>(64);

        _manager = new ObjectPoolManager();
        _managerTyped = _manager.GetTypedPool<PooledItem>();
        _manager.Prealloc<PooledItem>(64);
    }

    [BenchmarkCategory("ObjectPool"), Benchmark(Baseline = true)]
    public PooledItem PoolRentReturn()
    {
        PooledItem item = _pool.Get<PooledItem>();
        _pool.Return(item);
        return item;
    }

    [BenchmarkCategory("ObjectPool"), Benchmark]
    public PooledItem PoolTypedRentReturn()
    {
        PooledItem item = _poolTyped.Get();
        _poolTyped.Return(item);
        return item;
    }

    [BenchmarkCategory("ObjectPoolManager"), Benchmark]
    public PooledItem ManagerRentReturn()
    {
        PooledItem item = _manager.Get<PooledItem>();
        _manager.Return(item);
        return item;
    }

    [BenchmarkCategory("ObjectPoolManager"), Benchmark]
    public PooledItem ManagerTypedRentReturn()
    {
        PooledItem item = _managerTyped.Get();
        _managerTyped.Return(item);
        return item;
    }

    [BenchmarkCategory("Management"), Benchmark]
    public int PerformManagerHealthCheck() => _manager.PerformHealthCheck();

    public sealed class PooledItem : IPoolable
    {
        public int Value { get; set; }
        public void ResetForPool() => Value = 0;
    }
}
