// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
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
    public void ConstructorWithExplicitOptionsInitializesEmptyState()
    {
        using TaskManager manager = this.CreateManager();

        Assert.Equal("Workers: 0 running / 0 total | Recurring: 0", manager.Title);
        Assert.Equal(0, manager.WorkerErrorCount);
        Assert.Equal(0, manager.RecurringErrorCount);
        Assert.True(manager.AverageWorkerExecutionTime >= 0);
        Assert.True(manager.AverageRecurringExecutionTime >= 0);
    }

    [Fact]
    public void ConstructorParameterlessCreatesInstance()
    {
        using TaskManager manager = new();

        Assert.NotNull(manager);
    }

    [Fact]
    public async Task RunOnceAsyncWhenDelegateCompletesExecutesSuccessfully()
    {
        using TaskManager manager = this.CreateManager();
        bool executed = false;

        await manager.RunOnceAsync("run-once", ct =>
        {
            executed = true;
            return ValueTask.CompletedTask;
        });

        Assert.True(executed);
    }

    [Fact]
    public async Task RunOnceAsyncWhenDelegateThrowsRethrowsOriginalException()
    {
        using TaskManager manager = this.CreateManager();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await manager.RunOnceAsync("run-once-fail", _ => ValueTask.FromException(new InvalidOperationException("boom"))).ConfigureAwait(false));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task RunOnceAsyncWhenNameIsNullThrowsArgumentNullException()
    {
        using TaskManager manager = this.CreateManager();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await manager.RunOnceAsync(null!, _ => ValueTask.CompletedTask).ConfigureAwait(false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunOnceAsyncWhenNameIsWhitespaceThrowsArgumentException(string name)
    {
        using TaskManager manager = this.CreateManager();

        _ = await Assert.ThrowsAsync<ArgumentException>(
            async () => await manager.RunOnceAsync(name, _ => ValueTask.CompletedTask).ConfigureAwait(false));
    }

    [Fact]
    public void ScheduleWorkerWhenArgumentsAreInvalidThrowsExpectedException()
    {
        using TaskManager manager = this.CreateManager();

        _ = Assert.Throws<ArgumentException>(() => manager.ScheduleWorker("", "group", (_, _) => ValueTask.CompletedTask));
        _ = Assert.Throws<ArgumentNullException>(() => manager.ScheduleWorker("worker", "group", null!));
    }

    [Fact]
    public async Task ScheduleWorkerWhenWorkCompletesUpdatesHandleAndLookups()
    {
        using TaskManager manager = this.CreateManager();
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
        _ = Assert.NotNull(handle.LastHeartbeatUtc);

        Assert.True(manager.TryGetWorker(handle.Id, out IWorkerHandle? foundHandle));
        Assert.Same(handle, foundHandle);

        IReadOnlyCollection<IWorkerHandle> allWorkers = manager.GetWorkers(runningOnly: false);
        Assert.Contains(handle, allWorkers);
    }

    [Fact]
    public async Task ScheduleWorkerWhenWorkThrowsIncrementsWorkerErrorCount()
    {
        using TaskManager manager = this.CreateManager();

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
    public async Task CancelWorkerWhenWorkerExistsReturnsTrue()
    {
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> started = CreateCompletionSource<bool>();
        TaskCompletionSource<bool> cancelled = CreateCompletionSource<bool>();

        IWorkerHandle handle = manager.ScheduleWorker(
            "worker.cancel",
            "group-a",
            async (context, cancellationToken) =>
            {
                _ = started.TrySetResult(true);

                using CancellationTokenRegistration registration = cancellationToken.Register(() => cancelled.TrySetResult(true));

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            },
            new WorkerOptions
            {
                RetainFor = TimeSpan.FromMinutes(1)
            });

        _ = await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        bool cancelledWorker = manager.CancelWorker(handle.Id);

        Assert.True(cancelledWorker);
        _ = await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CancelWorkerWhenWorkerDoesNotExistReturnsFalse()
    {
        using TaskManager manager = this.CreateManager();

        bool cancelled = manager.CancelWorker(Identifiers.Snowflake.NewId(SnowflakeType.Unknown));

        Assert.False(cancelled);
    }

    [Fact]
    public async Task CancelAllWorkersAndCancelGroupReturnExpectedCounts()
    {
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> groupAStarted = CreateCompletionSource<bool>();
        TaskCompletionSource<bool> groupBStarted = CreateCompletionSource<bool>();

        _ = manager.ScheduleWorker(
            "worker.a",
            "group-a",
            async (context, cancellationToken) =>
            {
                _ = groupAStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = manager.ScheduleWorker(
            "worker.b",
            "group-b",
            async (context, cancellationToken) =>
            {
                _ = groupBStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = await Task.WhenAll(
            groupAStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)),
            groupBStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        int groupCancelled = manager.CancelGroup("group-a");
        int allCancelled = manager.CancelAllWorkers();

        Assert.Equal(1, groupCancelled);
        Assert.Equal(1, allCancelled);
    }

    [Fact]
    public async Task GetWorkersWhenFilteredByGroupAndRunningStateReturnsExpectedWorkers()
    {
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> runningWorkerStarted = CreateCompletionSource<bool>();

        IWorkerHandle completedWorker = manager.ScheduleWorker(
            "worker.completed",
            "group-a",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        IWorkerHandle runningWorker = manager.ScheduleWorker(
            "worker.running",
            "group-b",
            async (context, cancellationToken) =>
            {
                _ = runningWorkerStarted.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            },
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        _ = await runningWorkerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => !completedWorker.IsRunning, TimeSpan.FromSeconds(2));

        IReadOnlyCollection<IWorkerHandle> runningOnly = manager.GetWorkers();
        IReadOnlyCollection<IWorkerHandle> allInGroupA = manager.GetWorkers(runningOnly: false, group: "group-a");

        Assert.Contains(runningWorker, runningOnly);
        Assert.DoesNotContain(completedWorker, runningOnly);
        _ = Assert.Single(allInGroupA);
        Assert.Contains(completedWorker, allInGroupA);
    }

    [Fact]
    public void ScheduleRecurringWhenArgumentsAreInvalidThrowsExpectedException()
    {
        using TaskManager manager = this.CreateManager();

        _ = Assert.Throws<ArgumentException>(() => manager.ScheduleRecurring("", TimeSpan.FromMilliseconds(10), _ => ValueTask.CompletedTask));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => manager.ScheduleRecurring("recurring", TimeSpan.Zero, _ => ValueTask.CompletedTask));
        _ = Assert.Throws<ArgumentNullException>(() => manager.ScheduleRecurring("recurring", TimeSpan.FromMilliseconds(10), null!));
    }

    [Fact]
    public void ScheduleRecurringWhenNameAlreadyExistsThrowsAndIncrementsErrorCount()
    {
        using TaskManager manager = this.CreateManager();

        _ = manager.ScheduleRecurring(
            "recurring.duplicate",
            TimeSpan.FromMilliseconds(50),
            _ => ValueTask.CompletedTask,
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false
            });

        _ = Assert.Throws<InvalidOperationException>(() =>
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
    public async Task ScheduleRecurringWhenScheduledRunsLookupAndCancelWork()
    {
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> executed = CreateCompletionSource<bool>();

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

        Assert.True(manager.TryGetRecurring("recurring.run", out IRecurringHandle? foundHandle));
        Assert.Same(handle, foundHandle);
        Assert.Equal("recurring.run", handle.Name);
        Assert.True(handle.TotalRuns > 0);
        _ = Assert.NotNull(handle.LastRunUtc);
        _ = Assert.NotNull(handle.NextRunUtc);

        bool cancelled = manager.CancelRecurring("recurring.run");
        bool cancelledAgain = manager.CancelRecurring("recurring.run");

        Assert.True(cancelled);
        Assert.False(cancelledAgain);
    }

    [Fact]
    public void CancelRecurringWhenNameIsNullReturnsFalse()
    {
        using TaskManager manager = this.CreateManager();

        bool cancelled = manager.CancelRecurring(null);

        Assert.False(cancelled);
    }

    [Fact]
    public async Task GenerateReportWhenWorkersAndRecurringTasksExistContainsSummaryAndNames()
    {
        using TaskManager manager = this.CreateManager();
        TaskCompletionSource<bool> recurringExecuted = CreateCompletionSource<bool>();

        IWorkerHandle worker = manager.ScheduleWorker(
            "worker.report",
            "group-report",
            (_, _) => ValueTask.CompletedTask,
            new WorkerOptions { RetainFor = TimeSpan.FromMinutes(1) });

        IRecurringHandle recurring = manager.ScheduleRecurring(
            "recurring.report",
            TimeSpan.FromMilliseconds(50),
            cancellationToken =>
            {
                _ = recurringExecuted.TrySetResult(true);
                return ValueTask.CompletedTask;
            },
            new RecurringOptions
            {
                Jitter = TimeSpan.Zero,
                NonReentrant = false
            });

        _ = await recurringExecuted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => worker.TotalRuns == 1, TimeSpan.FromSeconds(2));

        string report = manager.GenerateReport();

        Assert.Contains("TaskManager:", report);
        Assert.Contains("recurring.report", report);
        Assert.Contains("Workers by Group:", report);
        Assert.Contains("Recurring:", report);
        Assert.Contains("group-report", report);
        Assert.Contains("worker.report", manager.GetWorkers(runningOnly: false).Select(static workerHandle => workerHandle.Name));
        Assert.True(manager.AverageWorkerExecutionTime >= 0);
        Assert.True(manager.AverageRecurringExecutionTime >= 0);

        _ = manager.CancelRecurring(recurring.Name);
    }

    [Fact]
    public void DisposeWhenCalledClearsTrackedStateAndPreventsFurtherScheduling()
    {
        TaskManager manager = this.CreateManager();

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
