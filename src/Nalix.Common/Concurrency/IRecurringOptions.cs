// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Nalix.Common.Concurrency;

/// <summary>
/// Provides configuration options for a recurring job.
/// </summary>
public interface IRecurringOptions
{
    /// <summary>
    /// Gets the tag associated with the recurring job for identification or grouping.
    /// </summary>
    [Required]
    string? Tag { get; init; }

    /// <summary>
    /// Gets the amount of random jitter to add to the job interval.
    /// Jitter helps to spread out job executions and avoid thundering herd problems.
    /// </summary>
    TimeSpan? Jitter { get; init; }

    /// <summary>
    /// Gets the maximum backoff duration to wait before retrying after failures.
    /// </summary>
    TimeSpan BackoffCap { get; init; }

    /// <summary>
    /// Gets the maximum duration allowed for each job run.
    /// If the job exceeds this timeout, it may be cancelled.
    /// </summary>
    TimeSpan? ExecutionTimeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether the recurring job should be non-reentrant.
    /// Non-reentrant jobs prevent overlapping executions.
    /// </summary>
    [DefaultValue(true)]
    bool NonReentrant { get; init; }

    /// <summary>
    /// Gets the maximum number of consecutive failures before applying backoff logic.
    /// </summary>
    [Range(0, int.MaxValue)]
    int FailuresBeforeBackoff { get; init; }
}
