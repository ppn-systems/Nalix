// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Threading;
using Nalix.Abstractions.Identity;

namespace Nalix.Abstractions.Concurrency;

/// <summary>
/// Provides configuration options for a worker, including retention, concurrency, and tagging.
/// </summary>
public interface IWorkerOptions
{
    /// <summary>
    /// Gets the optional tag associated with the worker.
    /// </summary>
    string? Tag { get; set; }

    /// <summary>
    /// Gets the optional machine identifier for the worker instance.
    /// </summary>
    ushort MachineId { get; set; }

    /// <summary>
    /// Gets the optional identifier type for the worker instance.
    /// </summary>
    SnowflakeType IdType { get; set; }

    /// <summary>
    /// Gets the dispatch priority for this worker while it is waiting in the scheduler queue.
    /// </summary>
    WorkerPriority Priority { get; set; }

    /// <summary>
    /// Gets the action to invoke when the worker has completed successfully.
    /// </summary>
    Action<IWorkerHandle>? OnCompleted { get; }

    /// <summary>
    /// Gets the action to invoke when the worker has failed.
    /// </summary>
    Action<IWorkerHandle, Exception>? OnFailed { get; }

    /// <summary>
    /// Gets the optional execution timeout for the worker.
    /// If set, the worker will be cancelled if it does not complete within this duration.
    /// </summary>
    TimeSpan? ExecutionTimeout { get; }

    /// <summary>
    /// Gets the duration for which finished workers are retained for diagnostics.
    /// Set to <c>null</c> or <c>TimeSpan.Zero</c> to auto-remove workers immediately.
    /// </summary>
    TimeSpan? RetainFor { get; set; }

    /// <summary>
    /// Gets the optional concurrency cap for workers in the same group.
    /// If set, executions in this group are limited by this value.
    /// </summary>
    int? GroupConcurrencyLimit { get; set; }

    /// <summary>
    /// Gets a value indicating whether the group slot should be acquired immediately or the worker should be cancelled if unavailable.
    /// Default is <c>false</c>, which means the worker will wait for a slot.
    /// </summary>
    [DefaultValue(false)]
    bool TryAcquireSlotImmediately { get; set; }

    /// <summary>
    /// Gets the cancellation token that is linked to the worker's execution.
    /// </summary>
    CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets the optional OS-level thread priority for the worker.
    /// If set, the worker will run on a dedicated thread with this priority.
    /// </summary>
    ThreadPriority? OSPriority { get; set; }

    /// <summary>
    /// Gets the optional preferred CPU core index (0-based) for thread affinity.
    /// When set along with <see cref="OSPriority"/>, the dedicated worker thread
    /// will be pinned to this specific logical processor.
    /// Ignored when the worker runs on the ThreadPool (OSPriority is null).
    /// If the value is out of range, the system performs a soft fallback (no pinning, no exception).
    /// </summary>
    int? ProcessorAffinity { get; set; }
}
