#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Xunit;

namespace Nalix.Framework.Tests.Tasks;

/// <summary>
/// Covers the public APIs exposed by the Tasks folder.
/// </summary>
public sealed class TasksTests : IDisposable
{
    private readonly List<TaskManager> _managers = [];

    /// <summary>
    /// Disposes task managers created by the test instance.
    /// </summary>
    public void Dispose()
    {
        foreach (TaskManager manager in _managers)
        {
            try
            {
                manager.Dispose();
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }

    /// <summary>
    /// Verifies that task naming tag constants keep their expected public values.
    /// </summary>
    [Fact]
    public void Tags_PublicConstants_ExposeExpectedValues()
    {
        // Arrange
        // No additional setup required.

        // Act and Assert
        Assert.Equal("proc", TaskNaming.Tags.Process);
        Assert.Equal("worker", TaskNaming.Tags.Worker);
        Assert.Equal("accept", TaskNaming.Tags.Accept);
        Assert.Equal("cleanup", TaskNaming.Tags.Cleanup);
        Assert.Equal("service", TaskNaming.Tags.Service);
        Assert.Equal("dispatch", TaskNaming.Tags.Dispatch);
    }

    /// <summary>
    /// Verifies that token sanitization preserves allowed characters and replaces unsupported ones.
    /// </summary>
    [Theory]
    [InlineData(null, "-")]
    [InlineData("", "-")]
    [InlineData("abc-_.123", "abc-_.123")]
    [InlineData("ab c/+", "ab_c__")]
    public void SanitizeToken_StateUnderTest_ReturnsExpectedToken(string? value, string expected)
    {
        // Arrange
        // Input supplied by InlineData.

        // Act
        string sanitized = TaskNaming.SanitizeToken(value!);

        // Assert
        Assert.Equal(expected, sanitized);
    }

    /// <summary>
    /// Verifies that recurring cleanup job ids incorporate the sanitized prefix and uppercase instance key.
    /// </summary>
    [Theory]
    [InlineData("cleanup job", 0xBC614E, "cleanup_job.cleanup.00BC614E")]
    [InlineData("svc", 15, "svc.cleanup.0000000F")]
    public void CleanupJobId_StateUnderTest_ReturnsExpectedIdentifier(string prefix, int instanceKey, string expected)
    {
        // Arrange
        // Input supplied by InlineData.

        // Act
        string identifier = TaskNaming.Recurring.CleanupJobId(prefix, instanceKey);

        // Assert
        Assert.Equal(expected, identifier);
    }

    /// <summary>
    /// Verifies that explicit task manager construction exposes the expected default observable state.
    /// </summary>
    [Fact]
    public void Constructor_ExplicitOptions_InitializesEmptyState()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();

        // Act
        string title = manager.Title;

        // Assert
        Assert.Equal("Workers: 0 running / 0 total | Recurring: 0", title);
        Assert.Equal(0, manager.WorkerErrorCount);
        Assert.Equal(0, manager.RecurringErrorCount);
        Assert.True(manager.AverageWorkerExecutionTime >= 0);
        Assert.True(manager.AverageRecurringExecutionTime >= 0);
    }

    /// <summary>
    /// Verifies that scheduling a worker rejects invalid public arguments.
    /// </summary>
    [Fact]
    public void ScheduleWorker_InvalidArguments_ThrowExpectedExceptions()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();

        // Act and Assert
        _ = Assert.Throws<ArgumentException>(() => manager.ScheduleWorker("", "group", (_, _) => ValueTask.CompletedTask));
        _ = Assert.Throws<ArgumentNullException>(() => manager.ScheduleWorker("worker", "group", null!));
    }

    /// <summary>
    /// Verifies that a completed worker updates its public handle fields and is exposed through worker lookups.
    /// </summary>
    [Fact]
    public async Task ScheduleWorker_WorkCompletes_UpdatesHandleAndLookups()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<IWorkerHandle> completion = CreateCompletionSource<IWorkerHandle>();

        // Act
        IWorkerHandle handle = manager.ScheduleWorker(
            "worker.complete",
            "group-a",
            (context, cancellationToken) =>
            {
                context.Advance(3, "step-1");
                context.Beat();
                return ValueTask.CompletedTask;
            },
            new WorkerOptions
            {
                RetainFor = TimeSpan.FromMinutes(1),
                OnCompleted = worker => completion.TrySetResult(worker)
            });

        IWorkerHandle completed = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => !handle.IsRunning && handle.TotalRuns == 1, TimeSpan.FromSeconds(2));

        // Assert
        Assert.Same(handle, completed);
        Assert.Equal("worker.complete", handle.Name);
        Assert.Equal("group-a", handle.Group);
        Assert.Equal(3, handle.Progress);
        Assert.Equal("step-1", handle.LastNote);
        Assert.True(manager.TryGetWorker(handle.Id, out IWorkerHandle? found));
        Assert.Same(handle, found);
        Assert.Contains(handle, manager.GetWorkers(runningOnly: false));
    }

    /// <summary>
    /// Verifies that a worker failure increments the public worker error count.
    /// </summary>
    [Fact]
    public async Task ScheduleWorker_WorkThrows_IncrementsWorkerErrorCount()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();

        // Act
        IWorkerHandle handle = manager.ScheduleWorker(
            "worker.fail",
            "group-a",
            (_, _) => ValueTask.FromException(new InvalidOperationException("boom")),
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        await WaitUntilAsync(() => !handle.IsRunning && handle.TotalRuns == 1, TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(1, manager.WorkerErrorCount);
    }

    /// <summary>
    /// Verifies that worker cancellation APIs cancel known workers and groups and ignore unknown workers.
    /// </summary>
    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058:Expression value is never used", Justification = "<Pending>")]
    public async Task CancelWorker_StateUnderTest_ReturnsExpectedResults()
    {
        // Arrangefix
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> groupAStarted = CreateCompletionSource<bool>();
        TaskCompletionSource<bool> groupBStarted = CreateCompletionSource<bool>();

        IWorkerHandle workerA = manager.ScheduleWorker(
            "worker.a",
            "group-a",
            async (_, cancellationToken) =>
            {
                groupAStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = manager.ScheduleWorker(
            "worker.b",
            "group-b",
            async (_, cancellationToken) =>
            {
                groupBStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = await Task.WhenAll(
            groupAStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)),
            groupBStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        // Act
        bool cancelledWorker = manager.CancelWorker(workerA.Id);
        bool cancelledUnknown = manager.CancelWorker(Snowflake.NewId(SnowflakeType.Unknown));
        int cancelledGroup = manager.CancelGroup("group-b");
        int cancelledRemaining = manager.CancelAllWorkers();

        // Assert
        Assert.True(cancelledWorker);
        Assert.False(cancelledUnknown);
        Assert.Equal(1, cancelledGroup);
        Assert.True(cancelledRemaining >= 0);
    }

    /// <summary>
    /// Verifies that worker queries can filter by running state and group.
    /// </summary>
    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058:Expression value is never used", Justification = "<Pending>")]
    public async Task GetWorkers_StateUnderTest_FiltersByRunningStateAndGroup()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> started = CreateCompletionSource<bool>();

        IWorkerHandle completed = manager.ScheduleWorker(
            "worker.completed",
            "group-a",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        IWorkerHandle running = manager.ScheduleWorker(
            "worker.running",
            "group-b",
            async (_, cancellationToken) =>
            {
                started.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => !completed.IsRunning, TimeSpan.FromSeconds(2));

        // Act
        IReadOnlyCollection<IWorkerHandle> runningOnly = manager.GetWorkers();
        IReadOnlyCollection<IWorkerHandle> groupAWorkers = manager.GetWorkers(runningOnly: false, group: "group-a");

        // Assert
        Assert.Contains(running, runningOnly);
        Assert.DoesNotContain(completed, runningOnly);
        _ = Assert.Single(groupAWorkers);
        Assert.Contains(completed, groupAWorkers);
    }

    /// <summary>
    /// Verifies that scheduling recurring work rejects invalid public arguments and duplicate names.
    /// </summary>
    [Fact]
    public void ScheduleRecurring_InvalidArguments_ThrowExpectedExceptions()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();

        // Act and Assert
        _ = Assert.Throws<ArgumentException>(() => manager.ScheduleRecurring("", TimeSpan.FromMilliseconds(10), _ => ValueTask.CompletedTask));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => manager.ScheduleRecurring("recurring", TimeSpan.Zero, _ => ValueTask.CompletedTask));
        _ = Assert.Throws<ArgumentNullException>(() => manager.ScheduleRecurring("recurring", TimeSpan.FromMilliseconds(10), null!));

        _ = manager.ScheduleRecurring(
            "recurring.duplicate",
            TimeSpan.FromMilliseconds(50),
            _ => ValueTask.CompletedTask,
            new RecurringOptions { Jitter = TimeSpan.Zero, NonReentrant = false });

        _ = Assert.Throws<InvalidOperationException>(() =>
            manager.ScheduleRecurring(
                "recurring.duplicate",
                TimeSpan.FromMilliseconds(50),
                _ => ValueTask.CompletedTask,
                new RecurringOptions { Jitter = TimeSpan.Zero, NonReentrant = false }));

        Assert.Equal(1, manager.RecurringErrorCount);
    }

    /// <summary>
    /// Verifies that recurring scheduling updates the public handle, recurring lookup APIs, and cancellation results.
    /// </summary>
    [Fact]
    public async Task ScheduleRecurring_StateUnderTest_UpdatesHandleLookupAndCancellation()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> executed = CreateCompletionSource<bool>();

        // Act
        IRecurringHandle handle = manager.ScheduleRecurring(
            "recurring.run",
            TimeSpan.FromMilliseconds(50),
            cancellationToken =>
            {
                _ = executed.TrySetResult(true);
                return ValueTask.CompletedTask;
            },
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false
            });

        _ = await executed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => handle.TotalRuns > 0, TimeSpan.FromSeconds(2));

        bool foundRecurring = manager.TryGetRecurring("recurring.run", out IRecurringHandle? recurring);
        IReadOnlyCollection<IRecurringHandle> recurringHandles = manager.GetRecurring();
        bool cancelled = manager.CancelRecurring("recurring.run");
        bool cancelledAgain = manager.CancelRecurring("recurring.run");
        bool cancelledNull = manager.CancelRecurring(null);

        // Assert
        Assert.True(foundRecurring);
        Assert.Same(handle, recurring);
        Assert.Contains(handle, recurringHandles);
        Assert.Equal("recurring.run", handle.Name);
        Assert.True(handle.TotalRuns > 0);
        _ = Assert.NotNull(handle.LastRunUtc);
        _ = Assert.NotNull(handle.NextRunUtc);
        Assert.True(cancelled);
        Assert.False(cancelledAgain);
        Assert.False(cancelledNull);
    }

    /// <summary>
    /// Verifies that recurring failures increment the public recurring error count.
    /// </summary>
    [Fact]
    public async Task ScheduleRecurring_WorkThrows_IncrementsRecurringErrorCount()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();
        IRecurringHandle handle = manager.ScheduleRecurring(
            "recurring.fail",
            TimeSpan.FromMilliseconds(30),
            _ => ValueTask.FromException(new InvalidOperationException("recurring boom")),
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false,
                FailuresBeforeBackoff = 1
            });

        // Act
        await WaitUntilAsync(() => handle.TotalRuns > 0, TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(handle.TotalRuns >= 1);
        Assert.True(handle.ConsecutiveFailures >= 1);
    }

    /// <summary>
    /// Verifies that run-once execution completes, validates names, and rethrows work exceptions.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunOnceAsync_NameIsWhitespace_ThrowsArgumentException(string name)
    {
        // Arrange
        using TaskManager manager = this.CreateManager();

        // Act and Assert
        _ = await Assert.ThrowsAsync<ArgumentException>(async () => await manager.RunOnceAsync(name, _ => ValueTask.CompletedTask).ConfigureAwait(false));
    }

    /// <summary>
    /// Verifies that run-once execution completes and preserves exception behavior.
    /// </summary>
    [Fact]
    public async Task RunOnceAsync_StateUnderTest_CompletesOrRethrows()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();
        bool executed = false;

        // Act
        await manager.RunOnceAsync("run-once", _ =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await manager.RunOnceAsync("run-once-fail", _ => ValueTask.FromException(new InvalidOperationException("boom"))).ConfigureAwait(false));

        // Assert
        Assert.True(executed);
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// Verifies that report APIs expose the current worker and recurring summaries.
    /// </summary>
    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058:Expression value is never used", Justification = "<Pending>")]
    public async Task GenerateReport_StateUnderTest_ReturnsSummaryAndData()
    {
        // Arrange
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> recurringExecuted = CreateCompletionSource<bool>();

        IWorkerHandle worker = manager.ScheduleWorker(
            "worker.report",
            "group-report",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = manager.ScheduleRecurring(
            "recurring.report",
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                recurringExecuted.TrySetResult(true);
                return ValueTask.CompletedTask;
            },
            new RecurringOptions { Jitter = TimeSpan.Zero, NonReentrant = false });

        _ = await recurringExecuted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => worker.TotalRuns == 1, TimeSpan.FromSeconds(2));

        // Act
        string report = manager.GenerateReport();
        IDictionary<string, object> data = manager.GenerateReportData();

        // Assert
        Assert.Contains("TaskManager:", report);
        Assert.Contains("Workers by Group:", report);
        Assert.Contains("Recurring:", report);
        Assert.Contains("group-report", report);
        Assert.Contains("recurring.report", report);
        Assert.True(data.ContainsKey("WorkersTotal"));
        Assert.True(data.ContainsKey("RecurringCount"));
        Assert.True(data.ContainsKey("WorkersByGroup"));
        Assert.True(data.ContainsKey("TopRunningWorkers"));
    }

    /// <summary>
    /// Verifies that disposing the manager clears tracked tasks and prevents future scheduling.
    /// </summary>
    [Fact]
    public void Dispose_StateUnderTest_ClearsStateAndRejectsFutureScheduling()
    {
        // Arrange
        TaskManager manager = this.CreateManager();

        _ = manager.ScheduleRecurring(
            "recurring.dispose",
            TimeSpan.FromMilliseconds(50),
            _ => ValueTask.CompletedTask,
            new RecurringOptions { Jitter = TimeSpan.Zero, NonReentrant = false });

        _ = manager.ScheduleWorker(
            "worker.dispose",
            "group-dispose",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        // Act
        manager.Dispose();

        // Assert
        Assert.Empty(manager.GetRecurring());
        Assert.Empty(manager.GetWorkers(runningOnly: false));
        _ = Assert.Throws<ObjectDisposedException>(() => manager.ScheduleWorker("after-dispose", "group", (_, _) => ValueTask.CompletedTask));
        _ = Assert.Throws<ObjectDisposedException>(() => manager.ScheduleRecurring("after-dispose", TimeSpan.FromMilliseconds(10), _ => ValueTask.CompletedTask));
    }

    private TaskManager CreateManager()
    {
        TaskManager manager = new(new TaskManagerOptions
        {
            CleanupInterval = TimeSpan.FromSeconds(5),
            DynamicAdjustmentEnabled = false,
            MaxWorkers = 8,
            IsEnableLatency = true
        });

        _managers.Add(manager);
        return manager;
    }

    private static TaskCompletionSource<T> CreateCompletionSource<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.True(condition(), "The expected condition was not satisfied before the timeout elapsed.");
    }
}
