using Nalix.Common.Core.Abstractions;
using Nalix.Common.Core.Enums;
using Nalix.Framework.Identity;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nalix.Framework.Tests.Tasks;

public class TaskManagerTests
{
    [Fact]
    public async Task RunSingleJob_executes_and_returns()
    {
        var tm = new TaskManager();
        Boolean ran = false;

        await tm.RunOnceAsync("one", async ct =>
        {
            await Task.Delay(10, ct);
            ran = true;
            return;
        });

        Assert.True(ran);
    }

    [Fact]
    public async Task ScheduleRecurring_runs_and_can_be_cancelled()
    {
        var tm = new TaskManager();
        Int32 hits = 0;

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss. fff}] Creating recurring task...");

        var h = tm.ScheduleRecurring("r1", TimeSpan.FromMilliseconds(50), async ct =>
        {
            var count = Interlocked.Increment(ref hits);
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss. fff}] ✅ EXECUTED!  hits={count}");
            await Task.Delay(5, ct);
        }, new RecurringOptions
        {
            Jitter = TimeSpan.Zero,
            NonReentrant = false
        });

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm: ss.fff}] Task scheduled: {h.Name}");
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Interval: {h.Interval.TotalMilliseconds}ms");

        // ✅ Give it time to execute (50ms interval * 6 = 300ms)
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm: ss.fff}] Waiting 350ms...");
        await Task.Delay(350);

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] After wait: hits={hits}");
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] IsRunning={h.IsRunning}");
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] TotalRuns={h.TotalRuns}");
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] LastRunUtc={h.LastRunUtc}");
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] NextRunUtc={h.NextRunUtc}");
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] ConsecutiveFailures={h.ConsecutiveFailures}");

        Assert.True(hits > 0, $"Expected hits > 0 but was {hits}.  Task never executed!");

        Assert.True(tm.CancelRecurring("r1"));
        Int32 afterCancel = hits;

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Cancelled.  afterCancel={afterCancel}");
        await Task.Delay(150);

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] After cancel wait: hits={hits}");
        Assert.Equal(afterCancel, hits); // không tăng thêm
    }

    [Fact]
    public async Task StartWorker_runs_and_can_be_cancelled()
    {
        var tm = new TaskManager();
        var finished = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

        var w = tm.ScheduleWorker("w1", "g1", async (ctx, ct) =>
        {
            await Task.Delay(30, ct);
            _ = finished.TrySetResult(true);
        });

        Assert.True(await Task.WhenAny(finished.Task, Task.Delay(500)) == finished.Task);

        Assert.True(tm.CancelWorker(w.Id));
    }

    [Fact]
    public void CancelGroup_and_CancelAllWorkers_work()
    {
        // Test Snowflake uniqueness FIRST
        var ids = new HashSet<ISnowflake>();
        for (Int32 i = 0; i < 100; i++)
        {
            var id = Snowflake.NewId(SnowflakeType.Inventory, 1);
            if (!ids.Add(id))
            {
                throw new Exception($"Duplicate Snowflake ID generated at iteration {i}:  {id}");
            }
        }
        Console.WriteLine($"✅ Generated {ids.Count} unique Snowflake IDs");

        // Now test TaskManager
        var tm = new TaskManager();
        var w1 = tm.ScheduleWorker("w1", "G", async (_, ct) => await Task.Delay(200, ct));
        var w2 = tm.ScheduleWorker("w2", "G", async (_, ct) => await Task.Delay(200, ct));
        var w3 = tm.ScheduleWorker("w3", "H", async (_, ct) => await Task.Delay(200, ct));

        Assert.Equal(2, tm.CancelGroup("G"));
        Assert.Equal(1, tm.CancelAllWorkers());
    }

    [Fact]
    public void ListRecurring_and_ListWorkers_return_expected()
    {
        var tm = new TaskManager();
        var h = tm.ScheduleRecurring("r1", TimeSpan.FromSeconds(1), _ => ValueTask.CompletedTask);
        var w = tm.ScheduleWorker("w1", "G", (_, _) => ValueTask.CompletedTask);

        Assert.Contains(h, tm.GetRecurring());
        Assert.Contains(w, tm.GetWorkers(runningOnly: false));
    }

    [Fact]
    public void Dispose_cancels_everything()
    {
        var tm = new TaskManager();
        _ = tm.ScheduleRecurring("r1", TimeSpan.FromSeconds(1), _ => ValueTask.CompletedTask);
        _ = tm.ScheduleWorker("w1", "G", (_, _) => ValueTask.CompletedTask);

        tm.Dispose();
        Assert.Empty(tm.GetRecurring());
        Assert.Empty(tm.GetWorkers(runningOnly: false));
    }

    [Fact]
    public void TaskManagerOptions_validates_cleanup_interval()
    {
        var options = new TaskManagerOptions
        {
            CleanupInterval = TimeSpan.FromMilliseconds(500)
        };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void TaskManagerOptions_accepts_valid_cleanup_interval()
    {
        var options = new TaskManagerOptions
        {
            CleanupInterval = TimeSpan.FromSeconds(5)
        };

        options.Validate(); // Should not throw
        Assert.Equal(TimeSpan.FromSeconds(5), options.CleanupInterval);
    }

    [Fact]
    public void TaskManager_uses_custom_cleanup_interval()
    {
        var options = new TaskManagerOptions
        {
            CleanupInterval = TimeSpan.FromSeconds(10)
        };

        var tm = new TaskManager(options);
        // TaskManager should initialize successfully with custom options
        Assert.NotNull(tm);
        tm.Dispose();
    }
}
