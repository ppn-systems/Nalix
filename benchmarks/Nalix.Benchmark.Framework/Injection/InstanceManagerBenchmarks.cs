using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.Injection;

namespace Nalix.Benchmark.Framework.Injection;

// Mock services for benchmarking
public sealed class BenchServiceA { }
public sealed class BenchServiceB { }
public sealed class BenchServiceC { }

public interface IBenchService { void NoOp(); }
public sealed class BenchServiceWithIface : IBenchService { public void NoOp() { } }

public sealed class BenchServiceWithArgs(int value, string name)
{
    public int Value { get; } = value;
    public string Name { get; } = name;
}

/// <summary>
/// Benchmarks for InstanceManager dependency injection performance.
/// </summary>
public class InstanceManagerBenchmarks : NalixBenchmarkBase
{
    private InstanceManager _manager = null!;
    private static readonly object?[] s_ctorArgs = [42, "bench"];

    [Params(10, 50)]
    public int PreloadCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _manager = InstanceManager.Instance;
        _manager.Clear(dispose: false);

        // Core registrations
        _manager.Register(new BenchServiceA());
        _manager.Register(new BenchServiceB());
        _manager.Register(new BenchServiceC());
        _manager.Register(new BenchServiceWithIface());
        _manager.Register(new BenchServiceWithArgs(42, "bench"));

        for (int i = 5; i < PreloadCount; i++)
        {
            _manager.Register(new BenchServiceWithArgs(i, $"preload_{i}"), registerInterfaces: false);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _manager?.Clear(dispose: false);

    [IterationSetup(Targets = [nameof(ReplaceExistingInstance), nameof(RegisterWithInterfaces), nameof(RegisterClassOnly)])]
    public void IterationSetup()
    {
        _manager.Register(new BenchServiceA());
        _manager.Register(new BenchServiceWithIface(), registerInterfaces: true);
        _manager.Register(new BenchServiceB());
    }

    [BenchmarkCategory("Register"), Benchmark(Description = "Register (Replace Existing)")]
    public void ReplaceExistingInstance() => _manager.Register(new BenchServiceA());

    [BenchmarkCategory("Register"), Benchmark(Description = "Register (With Interfaces)")]
    public void RegisterWithInterfaces() => _manager.Register(new BenchServiceWithIface(), registerInterfaces: true);

    [BenchmarkCategory("Register"), Benchmark(Description = "Register (Class Only)")]
    public void RegisterClassOnly() => _manager.Register(new BenchServiceB(), registerInterfaces: false);

    [BenchmarkCategory("Resolve"), Benchmark(Description = "Resolve (Generic Slot Hit)")]
    public BenchServiceA? ResolveGenericSlot() => _manager.GetOrCreateInstance<BenchServiceA>();

    [BenchmarkCategory("Resolve"), Benchmark(Description = "Resolve (Dictionary Hit)")]
    public BenchServiceC? ResolveDictionary() => _manager.GetOrCreateInstance<BenchServiceC>();

    [BenchmarkCategory("Resolve"), Benchmark(Description = "Resolve (Signature Cache Hit)")]
    public BenchServiceWithArgs? ResolveSignatureCache() => _manager.GetOrCreateInstance<BenchServiceWithArgs>(s_ctorArgs);

    [BenchmarkCategory("Access"), Benchmark(Description = "Access (Generic Slot Hit)")]
    public BenchServiceA? AccessGenericSlot() => _manager.GetExistingInstance<BenchServiceA>();

    [BenchmarkCategory("Access"), Benchmark(Description = "Access (ThreadLocal L1 Hit)")]
    public BenchServiceB? AccessThreadLocal() => _manager.GetExistingInstance<BenchServiceB>();

    [BenchmarkCategory("Access"), Benchmark(Description = "Access (Dictionary Fallback)")]
    public BenchServiceC? AccessDictionary() => _manager.GetExistingInstance<BenchServiceC>();
}
