// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Tasks.Options;

namespace Nalix.Framework.Tasks.Options;

/// <summary>
/// Provides configuration options for worker tasks.
/// </summary>
public sealed class WorkerOptions : IWorkerOptions
{
    /// <summary>
    /// Gets an optional tag for identifying the worker.
    /// </summary>
    public System.String? Tag { get; init; }

    /// <summary>
    /// Gets the optional machine identifier for the worker instance.
    /// </summary>
    public System.UInt16 MachineId { get; init; } = 1;

    /// <summary>
    /// Gets the optional identifier type for the worker instance.
    /// </summary>
    public IdentifierType IdType { get; init; } = IdentifierType.System;

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
    /// Gets the cancellation token that is linked to the worker's execution.
    /// </summary>
    public System.Threading.CancellationToken CancellationToken { get; init; } = default;
}
