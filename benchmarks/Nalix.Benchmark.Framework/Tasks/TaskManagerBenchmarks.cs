using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Benchmark.Framework.Tasks;

/// <summary>
/// Benchmarks for TaskManager operations including background worker scheduling, one-time task execution, and status reporting.
/// </summary>
public class TaskManagerBenchmarks : NalixBenchmarkBase
{
    private TaskManager _manager = null!;
    private readonly Func<IWorkerContext, CancellationToken, ValueTask> _noopWorker = (_, _) => ValueTask.CompletedTask;
    private readonly Func<CancellationToken, ValueTask> _noopTask = _ => ValueTask.CompletedTask;

    [GlobalSetup]
    public void Setup()
    {
        _manager = new TaskManager(new TaskManagerOptions
        {
            MaxWorkers = 100,
            CleanupInterval = TimeSpan.FromMinutes(1),
            DynamicAdjustmentEnabled = false
        });

        // Warm up with some workers to populate internal collections
        for (int i = 0; i < 10; i++)
        {
            _manager.ScheduleWorker($"warmup.{i}", "warmup", _noopWorker);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _manager.Dispose();

    /// <summary>Schedules a new background worker task.</summary>
    [BenchmarkCategory("Scheduling"), Benchmark(Baseline = true)]
    public void ScheduleWorker()
    {
        _manager.ScheduleWorker("bench.worker", "bench", _noopWorker);
    }

    /// <summary>Executes a one-time task asynchronously using the manager.</summary>
    [BenchmarkCategory("Execution"), Benchmark]
    public async ValueTask RunOnceAsync()
    {
        await _manager.RunOnceAsync("bench.run-once", _noopTask);
    }

    /// <summary>Generates a full text report of the current TaskManager state.</summary>
    [BenchmarkCategory("Diagnostics"), Benchmark]
    public string GenerateStatusReport()
    {
        return _manager.GenerateReport();
    }
}
