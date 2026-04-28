using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Environment.Configuration;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Benchmark.Framework.Configuration;

public sealed class BenchConfigA : ConfigurationLoader { }
public sealed class BenchConfigB : ConfigurationLoader { }
public sealed class BenchConfigC : ConfigurationLoader { }

/// <summary>
/// Benchmarks for ConfigurationManager including loading, caching, and reloading.
/// </summary>
public class ConfigurationBenchmarks : NalixBenchmarkBase
{
    private ConfigurationManager _manager = null!;
    private string _iniPathA = null!;
    private string _iniPathB = null!;

    [GlobalSetup]
    public void Setup()
    {
        _manager = ConfigurationManager.Instance;
        string configDir = Path.GetDirectoryName(_manager.ConfigFilePath)!;
        _ = Directory.CreateDirectory(configDir);

        _iniPathA = Path.Combine(configDir, "bench_a.ini");
        _iniPathB = Path.Combine(configDir, "bench_b.ini");

        File.WriteAllText(_iniPathA, "[Benchmark]\nKey=ValueA\n");
        File.WriteAllText(_iniPathB, "[Benchmark]\nKey=ValueB\n");

        _manager.SetConfigFilePath(_iniPathA, autoReload: false);
        _manager.ClearAll();

        _ = _manager.Get<BenchConfigA>();
        _ = _manager.Get<BenchConfigB>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _manager.ClearAll();
        if (File.Exists(_iniPathA)) File.Delete(_iniPathA);
        if (File.Exists(_iniPathB)) File.Delete(_iniPathB);
    }

    [IterationSetup(Target = nameof(GetCacheMiss))]
    public void ResetConfigC() => _manager.Remove<BenchConfigC>();

    [IterationSetup(Targets = [nameof(SetPathNoReload), nameof(SetPathWithReload)])]
    public void ResetToPathA()
    {
        if (!string.Equals(_manager.ConfigFilePath, _iniPathA, StringComparison.OrdinalIgnoreCase))
        {
            _manager.SetConfigFilePath(_iniPathA, autoReload: false);
        }
    }

    [BenchmarkCategory("Get"), Benchmark(Baseline = true)]
    public BenchConfigA GetCacheHit() => _manager.Get<BenchConfigA>();

    [BenchmarkCategory("Get"), Benchmark]
    public BenchConfigC GetCacheMiss() => _manager.Get<BenchConfigC>();

    [BenchmarkCategory("Status"), Benchmark]
    public bool IsLoaded() => _manager.IsLoaded<BenchConfigA>();

    [BenchmarkCategory("Lifecycle"), Benchmark]
    public void ReloadAll() => _manager.ReloadAll();

    [BenchmarkCategory("Path"), Benchmark]
    public void SetPathNoReload() => _manager.SetConfigFilePath(_iniPathB, autoReload: false);

    [BenchmarkCategory("Path"), Benchmark]
    public void SetPathWithReload() => _manager.SetConfigFilePath(_iniPathB, autoReload: true);
}