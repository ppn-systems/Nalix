// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Concurrency;

namespace Nalix.Framework.Options;

/// <summary>
/// Provides configuration options for scheduling recurring tasks.
/// </summary>
public sealed class RecurringOptions : IRecurringOptions
{
    /// <summary>
    /// Gets an optional tag for identifying the recurring task.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Gets a value indicating whether the recurring task is non-reentrant.
    /// </summary>
    public bool NonReentrant { get; init; } = true;

    /// <summary>
    /// Gets an optional jitter to randomize the start time of the recurring task.
    /// </summary>
    public TimeSpan? Jitter { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets an optional timeout for a single run, after which the run is cancelled.
    /// </summary>
    public TimeSpan? ExecutionTimeout { get; init; }

    /// <summary>
    /// Gets the number of consecutive failures before backoff is applied.
    /// </summary>
    public int FailuresBeforeBackoff { get; init; } = 1;

    /// <summary>
    /// Gets the maximum backoff duration after consecutive failures.
    /// </summary>
    public TimeSpan BackoffCap { get; init; } = TimeSpan.FromSeconds(15);
}
