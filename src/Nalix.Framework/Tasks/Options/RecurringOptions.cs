// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Tasks.Options;

namespace Nalix.Framework.Tasks.Options;

/// <summary>
/// Provides configuration options for scheduling recurring tasks.
/// </summary>
public sealed class RecurringOptions : IRecurringOptions
{
    /// <summary>
    /// Gets an optional tag for identifying the recurring task.
    /// </summary>
    public System.String? Tag { get; init; }

    /// <summary>
    /// Gets a value indicating whether the recurring task is non-reentrant.
    /// </summary>
    public System.Boolean NonReentrant { get; init; } = true;

    /// <summary>
    /// Gets an optional jitter to randomize the start time of the recurring task.
    /// </summary>
    public System.TimeSpan? Jitter { get; init; } = System.TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets an optional timeout for a single run, after which the run is cancelled.
    /// </summary>
    public System.TimeSpan? ExecutionTimeout { get; init; }

    /// <summary>
    /// Gets the number of consecutive failures before backoff is applied.
    /// </summary>
    public System.Int32 FailuresBeforeBackoff { get; init; } = 1;

    /// <summary>
    /// Gets the maximum backoff duration after consecutive failures.
    /// </summary>
    public System.TimeSpan BackoffCap { get; init; } = System.TimeSpan.FromSeconds(15);
}
