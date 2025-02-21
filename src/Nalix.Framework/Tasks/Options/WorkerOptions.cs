// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Tasks.Options;

namespace Nalix.Framework.Tasks.Options;

/// <summary>
/// Provides configuration options for worker tasks.
/// </summary>
public sealed class WorkerOptions : IWorkerOptions
{
    /// <summary>
    /// Gets the duration for which finished workers are retained for diagnostics.
    /// Set to <c>null</c> or <see cref="System.TimeSpan.Zero"/> to auto-remove.
    /// </summary>
    public System.TimeSpan? Retention { get; init; } = System.TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets the optional per-group concurrency cap. If set, executions in this group are gated.
    /// </summary>
    public System.Int32? MaxGroupConcurrency { get; init; }

    /// <summary>
    /// Gets a value indicating whether to acquire group slot immediately or cancel if unavailable. Default: false (wait).
    /// </summary>
    public System.Boolean TryAcquireGroupSlotImmediately { get; init; } = false;

    /// <summary>
    /// Gets an optional tag for identifying the worker.
    /// </summary>
    public System.String? Tag { get; init; }
}
