// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Identity;

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
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="work"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is less than or equal to zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a recurring job with the same name already exists.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    IRecurringHandle ScheduleRecurring(string name, TimeSpan interval, Func<CancellationToken, ValueTask> work, IRecurringOptions? options = null);

    /// <summary>
    /// Executes a single job once.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="work">The delegate representing the job work.</param>
    /// <param name="ct">Cancellation token (optional).</param>
    /// <returns>A ValueTask representing the job execution.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="work"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct"/> is cancelled while the job is executing.</exception>
    /// <exception cref="Exception">Propagates any exception thrown by <paramref name="work"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    ValueTask RunOnceAsync(string name, Func<CancellationToken, ValueTask> work, CancellationToken ct = default);

    /// <summary>
    /// Starts a long-running worker task.
    /// </summary>
    /// <param name="name">The name of the worker.</param>
    /// <param name="group">The group to which the worker belongs.</param>
    /// <param name="work">The delegate representing the worker's work.</param>
    /// <param name="options">Options for the worker (optional).</param>
    /// <returns>A handle to manage the worker.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="work"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the worker cannot be registered.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has already been disposed.</exception>
    IWorkerHandle ScheduleWorker(string name, string group, Func<IWorkerContext, CancellationToken, ValueTask> work, IWorkerOptions? options = null);

    /// <summary>
    /// Cancels all running workers.
    /// </summary>
    /// <returns>The number of workers cancelled.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when a matched worker's cancellation source has already been disposed.</exception>
    int CancelAllWorkers();

    /// <summary>
    /// Cancels a worker by identifier.
    /// </summary>
    /// <param name="id">The worker's identifier.</param>
    /// <returns>True if the worker was cancelled; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the matched worker's cancellation source has already been disposed.</exception>
    void CancelWorker(ISnowflake id);

    /// <summary>
    /// Cancels all workers in a group.
    /// </summary>
    /// <param name="group">The name of the group.</param>
    /// <returns>The number of workers cancelled.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when a matched worker's cancellation source has already been disposed.</exception>
    int CancelGroup(string group);

    /// <summary>
    /// Cancels a recurring job by name.
    /// </summary>
    /// <param name="name">The name of the recurring job.</param>
    /// <returns>True if the job was cancelled; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the matched recurring job's cancellation source has already been disposed.</exception>
    void CancelRecurring(string name);

    /// <summary>
    /// Lists all workers, optionally filtered by running status and/or group.
    /// </summary>
    /// <param name="runningOnly">Whether to list only running workers.</param>
    /// <param name="group">The group to filter by (optional).</param>
    /// <returns>A read-only collection of worker handles.</returns>
    IReadOnlyCollection<IWorkerHandle> GetWorkers(bool runningOnly = true, string? group = null);

    /// <summary>
    /// Tries to get a worker by identifier.
    /// </summary>
    /// <param name="id">The worker's identifier.</param>
    /// <param name="handle">The handle to the worker if found.</param>
    /// <returns>True if the worker was found; otherwise, false.</returns>
    bool TryGetWorker(ISnowflake id, [NotNullWhen(true)] out IWorkerHandle? handle);

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
    bool TryGetRecurring(string name, [NotNullWhen(true)] out IRecurringHandle? handle);

    /// <summary>
    /// Waits asynchronously for all workers in a specific group to complete.
    /// </summary>
    /// <param name="group">The name of the group to wait for.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A task that completes when all workers in the group have finished.</returns>
    Task WaitGroupAsync(string group, CancellationToken ct = default);
}
