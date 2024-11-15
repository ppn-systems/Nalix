// Copyright (c) 2025 PPN Corporation. All rights reserved.


// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Common.Tasks;

/// <summary>
/// Provides configuration options for a worker, including retention, concurrency, and tagging.
/// </summary>
public interface IWorkerOptions
{
    /// <summary>
    /// Gets the optional tag associated with the worker.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(1)]
    [System.ComponentModel.DataAnnotations.MaxLength(32)]
    System.String Tag { get; init; }

    /// <summary>
    /// Gets the optional machine identifier for the worker instance.
    /// </summary>
    System.UInt16 MachineId { get; init; }

    /// <summary>
    /// Gets the optional identifier type for the worker instance.
    /// </summary>
    IdentifierType IdType { get; init; }

    /// <summary>
    /// Gets the action to invoke when the worker has completed successfully.
    /// </summary>
    System.Action<IWorkerHandle> OnCompleted { get; }

    /// <summary>
    /// Gets the action to invoke when the worker has failed.
    /// </summary>
    System.Action<IWorkerHandle, System.Exception> OnFailed { get; }

    /// <summary>
    /// Gets the optional execution timeout for the worker.
    /// If set, the worker will be cancelled if it does not complete within this duration.
    /// </summary>
    System.TimeSpan? ExecutionTimeout { get; }

    /// <summary>
    /// Gets the duration for which finished workers are retained for diagnostics.
    /// Set to <c>null</c> or <c>TimeSpan.Zero</c> to auto-remove workers immediately.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:00", "1.00:00:00")]
    System.TimeSpan? RetainFor { get; init; }

    /// <summary>
    /// Gets the optional concurrency cap for workers in the same group.
    /// If set, executions in this group are limited by this value.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue)]
    System.Int32? GroupConcurrencyLimit { get; init; }

    /// <summary>
    /// Gets a value indicating whether the group slot should be acquired immediately or the worker should be cancelled if unavailable.
    /// Default is <c>false</c>, which means the worker will wait for a slot.
    /// </summary>
    [System.ComponentModel.DefaultValue(false)]
    System.Boolean TryAcquireSlotImmediately { get; init; }

    /// <summary>
    /// Gets the cancellation token that is linked to the worker's execution.
    /// </summary>
    System.Threading.CancellationToken CancellationToken { get; init; }
}