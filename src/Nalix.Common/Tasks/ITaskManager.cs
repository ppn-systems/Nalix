// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Tasks.Options;

namespace Nalix.Common.Tasks;

/// <summary>
/// Unified manager for background jobs and long-running workers:
/// - Recurring jobs: deadline-based ticks (Stopwatch), non-drift, non-reentrant (default), jitter, timeout, backoff.
/// - Workers: tracks thousands of long-running tasks (e.g., TCP accept/read loops), query counts by group,
///   cancellation by id/name/group, optional per-group concurrency cap, heartbeat and progress.
/// - Thread-safe, low allocation, server-grade reporting.
/// </summary>
public interface ITaskManager : System.IDisposable, IReportable
{
    /// <summary>
    /// Schedules a recurring job.
    /// </summary>
    /// <param name="name">The name of the recurring job.</param>
    /// <param name="interval">The time interval between executions.</param>
    /// <param name="work">The delegate representing the job work.</param>
    /// <param name="options">Options for the recurring job (optional).</param>
    /// <returns>A handle to manage the recurring job.</returns>
    IRecurringHandle ScheduleRecurring(
        System.String name,
        System.TimeSpan interval,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        IRecurringOptions options = null);

    /// <summary>
    /// Executes a single job once.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="work">The delegate representing the job work.</param>
    /// <param name="ct">Cancellation token (optional).</param>
    /// <returns>A ValueTask representing the job execution.</returns>
    System.Threading.Tasks.ValueTask RunOnceAsync(
        System.String name,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        System.Threading.CancellationToken ct = default);

    /// <summary>
    /// Cancels a recurring job by name.
    /// </summary>
    /// <param name="name">The name of the recurring job.</param>
    /// <returns>True if the job was cancelled; otherwise, false.</returns>
    System.Boolean CancelRecurring(System.String name);

    /// <summary>
    /// Starts a long-running worker task.
    /// </summary>
    /// <param name="name">The name of the worker.</param>
    /// <param name="group">The group to which the worker belongs.</param>
    /// <param name="work">The delegate representing the worker's work.</param>
    /// <param name="options">Options for the worker (optional).</param>
    /// <returns>A handle to manage the worker.</returns>
    IWorkerHandle StartWorker(
        System.String name,
        System.String group,
        System.Func<IWorkerContext, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> work,
        IWorkerOptions options = null);

    /// <summary>
    /// Cancels a worker by identifier.
    /// </summary>
    /// <param name="id">The worker's identifier.</param>
    /// <returns>True if the worker was cancelled; otherwise, false.</returns>
    System.Boolean CancelWorker(IIdentifier id);

    /// <summary>
    /// Cancels all workers in a group.
    /// </summary>
    /// <param name="group">The name of the group.</param>
    /// <returns>The number of workers cancelled.</returns>
    System.Int32 CancelGroup(System.String group);

    /// <summary>
    /// Cancels all running workers.
    /// </summary>
    /// <returns>The number of workers cancelled.</returns>
    System.Int32 CancelAllWorkers();

    /// <summary>
    /// Lists all workers, optionally filtered by running status and/or group.
    /// </summary>
    /// <param name="runningOnly">Whether to list only running workers.</param>
    /// <param name="group">The group to filter by (optional).</param>
    /// <returns>A read-only collection of worker handles.</returns>
    System.Collections.Generic.IReadOnlyCollection<IWorkerHandle> ListWorkers(
        System.Boolean runningOnly = true,
        System.String group = null);

    /// <summary>
    /// Tries to get a worker by identifier.
    /// </summary>
    /// <param name="id">The worker's identifier.</param>
    /// <param name="handle">The handle to the worker if found.</param>
    /// <returns>True if the worker was found; otherwise, false.</returns>
    System.Boolean TryGetWorker(IIdentifier id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out IWorkerHandle handle);

    /// <summary>
    /// Lists all recurring jobs.
    /// </summary>
    /// <returns>A read-only collection of recurring job handles.</returns>
    System.Collections.Generic.IReadOnlyCollection<IRecurringHandle> ListRecurring();

    /// <summary>
    /// Tries to get a recurring job by name.
    /// </summary>
    /// <param name="name">The name of the recurring job.</param>
    /// <param name="handle">The handle to the recurring job if found.</param>
    /// <returns>True if the recurring job was found; otherwise, false.</returns>
    System.Boolean TryGetRecurring(
        System.String name,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IRecurringHandle handle);
}