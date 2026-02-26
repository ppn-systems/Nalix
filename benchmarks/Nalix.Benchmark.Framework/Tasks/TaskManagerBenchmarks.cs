using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Benchmark.Framework.Tasks;

[Config(typeof(global::Nalix.Benchmark.Framework.BenchmarkConfig))]
public class TaskManagerBenchmarks
{
    private static readonly TaskManagerOptions s_options = new()
    {
        CleanupInterval = TimeSpan.FromSeconds(30),
        DynamicAdjustmentEnabled = false,
        MaxWorkers = 8,
        IsEnableLatency = false
    };

    [Benchmark]
    public async Task RunOnceAsync_NoOp()
    {
        using TaskManager manager = new(s_options);
        await manager.RunOnceAsync("bench.run-once", static _ => ValueTask.CompletedTask);
    }

    [Benchmark]
    public async Task ScheduleWorker_NoOp_AndWait()
    {
        using TaskManager manager = new(s_options);
        _ = manager.ScheduleWorker(
            "bench.worker",
            "bench",
            static (_, _) => ValueTask.CompletedTask,
            new WorkerOptions
            {
                RetainFor = TimeSpan.FromMinutes(1)
            });

        await SpinWaitAsync(() => manager.GetWorkers(runningOnly: false).Count > 0, TimeSpan.FromSeconds(2));
        await SpinWaitAsync(
            () => manager.GetWorkers(runningOnly: false).Any(static worker => worker.TotalRuns > 0),
            TimeSpan.FromSeconds(2));
    }

    [Benchmark]
    public string GenerateReport_WithTrackedEntries()
    {
        using TaskManager manager = new(s_options);
        _ = manager.ScheduleWorker(
            "bench.report.worker",
            "bench",
            static (_, _) => ValueTask.CompletedTask,
            new WorkerOptions
            {
                RetainFor = TimeSpan.FromMinutes(1)
            });

        SpinWaitAsync(() => manager.GetWorkers(runningOnly: false).Count > 0, TimeSpan.FromSeconds(2))
            .GetAwaiter()
            .GetResult();

        return manager.GenerateReport();
    }

    private static async Task SpinWaitAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        throw new TimeoutException("Benchmark setup timed out while waiting for TaskManager state.");
    }
}
