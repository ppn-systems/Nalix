// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
#nullable enable

using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nalix.Framework.Tests.Tasks;

/// <summary>
/// Provides unit tests for the public API exposed by <see cref="TaskManager"/>.
/// </summary>
public sealed class TaskManagerTests : IDisposable
{
    private readonly List<TaskManager> _managers = [];

    /// <inheritdoc />
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
                // Test cleanup should be best-effort.
            }
        }
    }

    [Fact]
    public void Constructor_WithExplicitOptions_InitializesEmptyState()
    {
        using TaskManager manager = CreateManager();

        Assert.Equal("Workers: 0 running / 0 total | Recurring: 0", manager.Title);
        Assert.Equal(0, manager.WorkerErrorCount);
        Assert.Equal(0, manager.RecurringErrorCount);
        Assert.True(manager.AverageWorkerExecutionTime >= 0);
        Assert.True(manager.AverageRecurringExecutionTime >= 0);
    }

    [Fact]
    public void Constructor_Parameterless_CreatesInstance()
    {
        using TaskManager manager = new();

        Assert.NotNull(manager);
    }

    [Fact]
    public async Task RunOnceAsync_WhenDelegateCompletes_ExecutesSuccessfully()
    {
        using TaskManager manager = CreateManager();
        Boolean executed = false;

        await manager.RunOnceAsync("run-once", ct =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        });

        Assert.True(executed);
    }

    [Fact]
    public async Task RunOnceAsync_WhenDelegateThrows_RethrowsOriginalException()
    {
        using TaskManager manager = CreateManager();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await manager.RunOnceAsync("run-once-fail", _ => ValueTask.FromException(new InvalidOperationException("boom"))));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task RunOnceAsync_WhenNameIsNull_ThrowsArgumentNullException()
    {
        using TaskManager manager = CreateManager();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await manager.RunOnceAsync(null!, _ => ValueTask.CompletedTask));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunOnceAsync_WhenNameIsWhitespace_ThrowsArgumentException(String name)
    {
        using TaskManager manager = CreateManager();

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await manager.RunOnceAsync(name, _ => ValueTask.CompletedTask));
    }

    [Fact]
    public void ScheduleWorker_WhenArgumentsAreInvalid_ThrowsExpectedException()
    {
        using TaskManager manager = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.ScheduleWorker("", "group", (_, _) => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => manager.ScheduleWorker("worker", "group", null!));
    }

    [Fact]
    public async Task ScheduleWorker_WhenWorkCompletes_UpdatesHandleAndLookups()
    {
        using TaskManager manager = CreateManager();
        TaskCompletionSource<IWorkerHandle> completion = CreateCompletionSource<IWorkerHandle>();

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

        IWorkerHandle completedHandle = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(handle, completedHandle);

        await WaitUntilAsync(
            () => !handle.IsRunning && handle.TotalRuns == 1,
            TimeSpan.FromSeconds(2));

        Assert.Equal("worker.complete", handle.Name);
        Assert.Equal("group-a", handle.Group);
        Assert.Equal(3, handle.Progress);
        Assert.Equal("step-1", handle.LastNote);
        Assert.True(handle.StartedUtc <= DateTimeOffset.UtcNow);
        Assert.NotNull(handle.LastHeartbeatUtc);

        Assert.True(manager.TryGetWorker(handle.Id, out IWorkerHandle? foundHandle));
        Assert.Same(handle, foundHandle);

        IReadOnlyCollection<IWorkerHandle> allWorkers = manager.GetWorkers(runningOnly: false);
        Assert.Contains(handle, allWorkers);
    }

    [Fact]
    public async Task ScheduleWorker_WhenWorkThrows_IncrementsWorkerErrorCount()
    {
        using TaskManager manager = CreateManager();

        IWorkerHandle handle = manager.ScheduleWorker(
            "worker.fail",
            "group-a",
            (_, _) => ValueTask.FromException(new InvalidOperationException("worker failure")),
            new WorkerOptions
            {
                RetainFor = TimeSpan.FromMinutes(1)
            });

        await WaitUntilAsync(
            () => !handle.IsRunning && handle.TotalRuns == 1,
            TimeSpan.FromSeconds(2));

        Assert.Equal(1, manager.WorkerErrorCount);
    }

    [Fact]
    public async Task CancelWorker_WhenWorkerExists_ReturnsTrue()
    {
        using TaskManager manager = CreateManager();
        TaskCompletionSource<Boolean> started = CreateCompletionSource<Boolean>();
        TaskCompletionSource<Boolean> cancelled = CreateCompletionSource<Boolean>();

        IWorkerHandle handle = manager.ScheduleWorker(
            "worker.cancel",
            "group-a",
            async (_, cancellationToken) =>
            {
                started.TrySetResult(true);

                using CancellationTokenRegistration registration = cancellationToken.Register(() => cancelled.TrySetResult(true));

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            },
            new WorkerOptions
            {
                RetainFor = TimeSpan.FromMinutes(1)
            });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Boolean cancelledWorker = manager.CancelWorker(handle.Id);

        Assert.True(cancelledWorker);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CancelWorker_WhenWorkerDoesNotExist_ReturnsFalse()
    {
        using TaskManager manager = CreateManager();

        Boolean cancelled = manager.CancelWorker(Nalix.Framework.Identifiers.Snowflake.NewId(SnowflakeType.Unknown));

        Assert.False(cancelled);
    }

    [Fact]
    public async Task CancelAllWorkers_And_CancelGroup_ReturnExpectedCounts()
    {
        using TaskManager manager = CreateManager();
        TaskCompletionSource<Boolean> groupAStarted = CreateCompletionSource<Boolean>();
        TaskCompletionSource<Boolean> groupBStarted = CreateCompletionSource<Boolean>();

        _ = manager.ScheduleWorker(
            "worker.a",
            "group-a",
            async (_, cancellationToken) =>
            {
                groupAStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = manager.ScheduleWorker(
            "worker.b",
            "group-b",
            async (_, cancellationToken) =>
            {
                groupBStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        await Task.WhenAll(
            groupAStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)),
            groupBStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        Int32 groupCancelled = manager.CancelGroup("group-a");
        Int32 allCancelled = manager.CancelAllWorkers();

        Assert.Equal(1, groupCancelled);
        Assert.Equal(1, allCancelled);
    }

    [Fact]
    public async Task GetWorkers_WhenFilteredByGroupAndRunningState_ReturnsExpectedWorkers()
    {
        using TaskManager manager = CreateManager();
        TaskCompletionSource<Boolean> runningWorkerStarted = CreateCompletionSource<Boolean>();

        IWorkerHandle completedWorker = manager.ScheduleWorker(
            "worker.completed",
            "group-a",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        IWorkerHandle runningWorker = manager.ScheduleWorker(
            "worker.running",
            "group-b",
            async (_, cancellationToken) =>
            {
                runningWorkerStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        await runningWorkerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => !completedWorker.IsRunning, TimeSpan.FromSeconds(2));

        IReadOnlyCollection<IWorkerHandle> runningOnly = manager.GetWorkers();
        IReadOnlyCollection<IWorkerHandle> allInGroupA = manager.GetWorkers(runningOnly: false, group: "group-a");

        Assert.Contains(runningWorker, runningOnly);
        Assert.DoesNotContain(completedWorker, runningOnly);
        Assert.Single(allInGroupA);
        Assert.Contains(completedWorker, allInGroupA);
    }

    [Fact]
    public void ScheduleRecurring_WhenArgumentsAreInvalid_ThrowsExpectedException()
    {
        using TaskManager manager = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.ScheduleRecurring("", TimeSpan.FromMilliseconds(10), _ => ValueTask.CompletedTask));
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.ScheduleRecurring("recurring", TimeSpan.Zero, _ => ValueTask.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => manager.ScheduleRecurring("recurring", TimeSpan.FromMilliseconds(10), null!));
    }

    [Fact]
    public void ScheduleRecurring_WhenNameAlreadyExists_ThrowsAndIncrementsErrorCount()
    {
        using TaskManager manager = CreateManager();

        _ = manager.ScheduleRecurring(
            "recurring.duplicate",
            TimeSpan.FromMilliseconds(50),
            _ => ValueTask.CompletedTask,
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false
            });

        Assert.Throws<InvalidOperationException>(() =>
            manager.ScheduleRecurring(
                "recurring.duplicate",
                TimeSpan.FromMilliseconds(50),
                _ => ValueTask.CompletedTask,
                new RecurringOptions
                {
                    Jitter = TimeSpan.Zero,
                    NonReentrant = false
                }));

        Assert.Equal(1, manager.RecurringErrorCount);
    }

    [Fact]
    public async Task ScheduleRecurring_WhenScheduled_RunsLookupAndCancelWork()
    {
        using TaskManager manager = CreateManager();
        TaskCompletionSource<Boolean> executed = CreateCompletionSource<Boolean>();

        IRecurringHandle handle = manager.ScheduleRecurring(
            "recurring.run",
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                executed.TrySetResult(true);
                return ValueTask.CompletedTask;
            },
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false
            });

        await executed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => handle.TotalRuns > 0, TimeSpan.FromSeconds(2));

        Assert.True(manager.TryGetRecurring("recurring.run", out IRecurringHandle? foundHandle));
        Assert.Same(handle, foundHandle);
        Assert.Equal("recurring.run", handle.Name);
        Assert.True(handle.TotalRuns > 0);
        Assert.NotNull(handle.LastRunUtc);
        Assert.NotNull(handle.NextRunUtc);

        Boolean cancelled = manager.CancelRecurring("recurring.run");
        Boolean cancelledAgain = manager.CancelRecurring("recurring.run");

        Assert.True(cancelled);
        Assert.False(cancelledAgain);
    }

    [Fact]
    public void CancelRecurring_WhenNameIsNull_ReturnsFalse()
    {
        using TaskManager manager = CreateManager();

        Boolean cancelled = manager.CancelRecurring(null);

        Assert.False(cancelled);
    }

    [Fact]
    public async Task GenerateReport_WhenWorkersAndRecurringTasksExist_ContainsSummaryAndNames()
    {
        using TaskManager manager = CreateManager();
        TaskCompletionSource<Boolean> recurringExecuted = CreateCompletionSource<Boolean>();

        IWorkerHandle worker = manager.ScheduleWorker(
            "worker.report",
            "group-report",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        IRecurringHandle recurring = manager.ScheduleRecurring(
            "recurring.report",
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                recurringExecuted.TrySetResult(true);
                return ValueTask.CompletedTask;
            },
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false
            });

        await recurringExecuted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => worker.TotalRuns == 1, TimeSpan.FromSeconds(2));

        String report = manager.GenerateReport();

        Assert.Contains("TaskManager:", report);
        Assert.Contains("recurring.report", report);
        Assert.Contains("Workers by Group:", report);
        Assert.Contains("Recurring:", report);
        Assert.Contains("group-report", report);
        Assert.Contains("worker.report", manager.GetWorkers(runningOnly: false).Select(static workerHandle => workerHandle.Name));
        Assert.True(manager.AverageWorkerExecutionTime >= 0);
        Assert.True(manager.AverageRecurringExecutionTime >= 0);

        manager.CancelRecurring(recurring.Name);
    }

    [Fact]
    public void Dispose_WhenCalled_ClearsTrackedStateAndPreventsFurtherScheduling()
    {
        TaskManager manager = CreateManager();

        _ = manager.ScheduleRecurring(
            "recurring.dispose",
            TimeSpan.FromMilliseconds(50),
            _ => ValueTask.CompletedTask,
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false
            });

        _ = manager.ScheduleWorker(
            "worker.dispose",
            "group-dispose",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        manager.Dispose();

        Assert.Empty(manager.GetRecurring());
        Assert.Empty(manager.GetWorkers(runningOnly: false));
        Assert.Throws<ObjectDisposedException>(() => manager.ScheduleWorker("after-dispose", "group", (_, _) => ValueTask.CompletedTask));
        Assert.Throws<ObjectDisposedException>(() => manager.ScheduleRecurring("after-dispose", TimeSpan.FromMilliseconds(10), _ => ValueTask.CompletedTask));
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

    private static async Task WaitUntilAsync(Func<Boolean> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition(), "The expected condition was not satisfied before the timeout elapsed.");
    }
}
