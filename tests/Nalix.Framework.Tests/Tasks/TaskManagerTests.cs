using Nalix.Framework.Tasks;
using System;
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

        var h = tm.ScheduleRecurring("r1", TimeSpan.FromMilliseconds(50), async ct =>
        {
            _ = Interlocked.Increment(ref hits);
            await Task.Delay(5, ct);
        });

        await Task.Delay(300);
        Assert.True(hits > 0);

        Assert.True(tm.CancelRecurring("r1"));
        Int32 afterCancel = hits;
        await Task.Delay(150);
        Assert.Equal(afterCancel, hits); // không tăng thêm
    }

    [Fact]
    public async Task StartWorker_runs_and_can_be_cancelled()
    {
        var tm = new TaskManager();
        var finished = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

        var w = tm.StartWorker("w1", "g1", async (ctx, ct) =>
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
        var tm = new TaskManager();
        var w1 = tm.StartWorker("w1", "G", async (ctx, ct) => await Task.Delay(200, ct));
        var w2 = tm.StartWorker("w2", "G", async (ctx, ct) => await Task.Delay(200, ct));
        var w3 = tm.StartWorker("w3", "H", async (ctx, ct) => await Task.Delay(200, ct));

        Assert.Equal(2, tm.CancelGroup("G"));
        Assert.Equal(1, tm.CancelAllWorkers());
    }

    [Fact]
    public void ListRecurring_and_ListWorkers_return_expected()
    {
        var tm = new TaskManager();
        var h = tm.ScheduleRecurring("r1", TimeSpan.FromSeconds(1), ct => ValueTask.CompletedTask);
        var w = tm.StartWorker("w1", "G", (ctx, ct) => ValueTask.CompletedTask);

        Assert.Contains(h, tm.ListRecurring());
        Assert.Contains(w, tm.ListWorkers(runningOnly: false));
    }

    [Fact]
    public void Dispose_cancels_everything()
    {
        var tm = new TaskManager();
        _ = tm.ScheduleRecurring("r1", TimeSpan.FromSeconds(1), ct => ValueTask.CompletedTask);
        _ = tm.StartWorker("w1", "G", (ctx, ct) => ValueTask.CompletedTask);

        tm.Dispose();
        Assert.Empty(tm.ListRecurring());
        Assert.Empty(tm.ListWorkers(runningOnly: false));
    }
}
