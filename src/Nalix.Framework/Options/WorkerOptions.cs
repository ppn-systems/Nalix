// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;

namespace Nalix.Framework.Options;

/// <summary>
/// Provides configuration options for worker tasks.
/// </summary>
public sealed class WorkerOptions : IWorkerOptions
{
    /// <summary>
    /// Gets an optional tag for identifying the worker.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Gets the optional machine identifier for the worker instance.
    /// </summary>
    public ushort MachineId { get; init; } = 1;

    /// <summary>
    /// Gets the optional identifier type for the worker instance.
    /// </summary>
    public SnowflakeType IdType { get; init; } = SnowflakeType.System;

    /// <summary>
    /// Gets the scheduler priority for this worker while it is queued.
    /// </summary>
    public WorkerPriority Priority { get; init; } = WorkerPriority.NORMAL;

    /// <summary>
    /// Gets the optional execution timeout for the worker instance.
    /// If set, the worker will be cancelled if execution exceeds this duration.
    /// </summary>
    public TimeSpan? ExecutionTimeout { get; }

    /// <summary>
    /// Gets the duration for which finished workers are retained for diagnostics.
    /// Set to <c>null</c> or <see cref="TimeSpan.Zero"/> to auto-remove.
    /// </summary>
    public TimeSpan? RetainFor { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets the optional per-group concurrency cap. If set, executions in this group are gated.
    /// </summary>
    public int? GroupConcurrencyLimit { get; init; }

    /// <summary>
    /// Gets a value indicating whether to acquire group slot immediately or cancel if unavailable. Default: false (wait).
    /// </summary>
    public bool TryAcquireSlotImmediately { get; init; }

    /// <summary>
    /// Gets the cancellation token that is linked to the worker's execution.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the action to invoke when the worker has completed successfully.
    /// </summary>
    public Action<IWorkerHandle>? OnCompleted { get; set; }

    /// <summary>
    /// Gets the action to invoke when the worker has failed.
    /// </summary>
    public Action<IWorkerHandle, Exception>? OnFailed { get; set; }

    /// <summary>
    /// Gets the optional OS-level thread priority for the worker.
    /// </summary>
    public ThreadPriority? OSPriority { get; init; }
}
