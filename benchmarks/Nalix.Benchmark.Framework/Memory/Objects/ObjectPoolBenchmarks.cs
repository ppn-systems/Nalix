using BenchmarkDotNet.Attributes;
using Nalix.Common.Abstractions;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Memory.Pools;

namespace Nalix.Benchmark.Framework.Memory.Objects;

[MemoryDiagnoser]
[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class ObjectPoolBenchmarks
{
    private ObjectPool _pool = null!;
    private TypedObjectPool<PooledNode> _typedPool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new ObjectPool(128);
        _typedPool = _pool.CreateTypedPool<PooledNode>();
        _ = _pool.Prealloc<PooledNode>(32);
    }

    [Benchmark]
    public PooledNode Get_Return()
    {
        PooledNode node = _pool.Get<PooledNode>();
        _pool.Return(node);
        return node;
    }

    [Benchmark]
    public PooledNode TypedPool_Get_Return()
    {
        PooledNode node = _typedPool.Get();
        _typedPool.Return(node);
        return node;
    }

    [Benchmark]
    public int Prealloc()
        => _pool.Prealloc<PooledNode>(8);

    public sealed class PooledNode : IPoolable
    {
        public int Value { get; set; }

        public void ResetForPool() => Value = 0;
    }
}
