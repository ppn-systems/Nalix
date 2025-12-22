// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Identity;
using Nalix.Common.Shared;

namespace Nalix.Common.Concurrency;

/// <summary>
/// Unified manager for background jobs and long-running workers:
/// - Recurring jobs: deadline-based ticks (Stopwatch), non-drift, non-reentrant (default), jitter, timeout, backoff.
/// - Workers: tracks thousands of long-running tasks (e.g., TCP accept/read loops), query counts by group,
///   cancellation by id/name/group, optional per-group concurrency cap, heartbeat and progress.
/// - Thread-safe, low allocation, server-grade reporting.
/// </summary>
public interface ITaskManager : IDisposable, IReportable
{
    /// <summary>
    /// Gets a short console title summary containing running workers, total workers, and recurring tasks.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Schedules a recurring job.
    /// </summary>
    /// <param name="name">The name of the recurring job.</param>
    /// <param name="interval">The time interval between executions.</param>
    /// <param name="work">The delegate representing the job work.</param>
    /// <param name="options">Options for the recurring job (optional).</param>
    /// <returns>A handle to manage the recurring job.</returns>
    IRecurringHandle ScheduleRecurring(
        [NotNull] string name,
        [NotNull] TimeSpan interval,
        [NotNull]
        Func<CancellationToken, ValueTask> work,
        [MaybeNull] IRecurringOptions options = null);

    /// <summary>
    /// Executes a single job once.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="work">The delegate representing the job work.</param>
    /// <param name="ct">Cancellation token (optional).</param>
    /// <returns>A ValueTask representing the job execution.</returns>
    ValueTask RunOnceAsync(
        [NotNull] string name,
        [NotNull]
        Func<CancellationToken, ValueTask> work,
        [NotNull] CancellationToken ct = default);

    /// <summary>
    /// Starts a long-running worker task.
    /// </summary>
    /// <param name="name">The name of the worker.</param>
    /// <param name="group">The group to which the worker belongs.</param>
    /// <param name="work">The delegate representing the worker's work.</param>
    /// <param name="options">Options for the worker (optional).</param>
    /// <returns>A handle to manage the worker.</returns>
    IWorkerHandle ScheduleWorker(
        [NotNull] string name,
        [NotNull] string group,
        [NotNull]
        Func<IWorkerContext, CancellationToken, ValueTask> work,
        [MaybeNull] IWorkerOptions options = null);

    /// <summary>
    /// Cancels all running workers.
    /// </summary>
    /// <returns>The number of workers cancelled.</returns>
    int CancelAllWorkers();

    /// <summary>
    /// Cancels a worker by identifier.
    /// </summary>
    /// <param name="id">The worker's identifier.</param>
    /// <returns>True if the worker was cancelled; otherwise, false.</returns>
    bool CancelWorker([NotNull] ISnowflake id);

    /// <summary>
    /// Cancels all workers in a group.
    /// </summary>
    /// <param name="group">The name of the group.</param>
    /// <returns>The number of workers cancelled.</returns>
    int CancelGroup([NotNull] string group);

    /// <summary>
    /// Cancels a recurring job by name.
    /// </summary>
    /// <param name="name">The name of the recurring job.</param>
    /// <returns>True if the job was cancelled; otherwise, false.</returns>
    bool CancelRecurring([MaybeNull] string name);

    /// <summary>
    /// Lists all workers, optionally filtered by running status and/or group.
    /// </summary>
    /// <param name="runningOnly">Whether to list only running workers.</param>
    /// <param name="group">The group to filter by (optional).</param>
    /// <returns>A read-only collection of worker handles.</returns>
    IReadOnlyCollection<IWorkerHandle> GetWorkers(
        [NotNull] bool runningOnly = true,
        [MaybeNull] string group = null);

    /// <summary>
    /// Tries to get a worker by identifier.
    /// </summary>
    /// <param name="id">The worker's identifier.</param>
    /// <param name="handle">The handle to the worker if found.</param>
    /// <returns>True if the worker was found; otherwise, false.</returns>
    bool TryGetWorker(
        [NotNull] ISnowflake id,
        [NotNullWhen(true)] out IWorkerHandle handle);

    /// <summary>
    /// Lists all recurring jobs.
    /// </summary>
    /// <returns>A read-only collection of recurring job handles.</returns>
    IReadOnlyCollection<IRecurringHandle> GetRecurring();

    /// <summary>
    /// Tries to get a recurring job by name.
    /// </summary>
    /// <param name="name">The name of the recurring job.</param>
    /// <param name="handle">The handle to the recurring job if found.</param>
    /// <returns>True if the recurring job was found; otherwise, false.</returns>
    bool TryGetRecurring(
        [NotNull] string name,
        [NotNullWhen(true)] out IRecurringHandle handle);
}
