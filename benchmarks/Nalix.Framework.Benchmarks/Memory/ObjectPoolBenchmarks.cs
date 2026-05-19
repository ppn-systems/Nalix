using System;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions;
using Nalix.Environment.Configuration;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Benchmarks.Shared;

namespace Nalix.Framework.Benchmarks.Memory;

[Config(typeof(NalixBenchmarkConfig))]
public class ObjectPoolBenchmarks
{
    private ObjectPoolManager _poolManager = null!;

    public class SamplePoolable : IPoolable
    {
        public int Value1 { get; set; }
        public string? Value2 { get; set; }

        public void ResetForPool()
        {
            Value1 = 0;
            Value2 = null;
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        // Disable trimming to avoid scheduling recurring jobs that depend on TaskManager/InstanceManager
        var options = ConfigurationManager.Instance.Get<ObjectPoolOptions>();
        options.EnableObjectTrimming = false;
        options.EnableDiagnostics = false;
        options.EnableLeakDetection = false;

        _poolManager = new ObjectPoolManager(options);
    }

    [Benchmark(Baseline = true)]
    public SamplePoolable RawAllocation()
    {
        return new SamplePoolable { Value1 = 42, Value2 = "baseline" };
    }

    [Benchmark]
    public void RentAndReturn_ObjectPool()
    {
        var obj = _poolManager.Get<SamplePoolable>();
        obj.Value1 = 42;
        obj.Value2 = "pooled";
        _poolManager.Return(obj);
    }
}
