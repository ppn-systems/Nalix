// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Concurrency;

/// <summary>
/// Provides configuration options for a recurring job.
/// </summary>
public interface IRecurringOptions
{
    /// <summary>
    /// Gets the tag associated with the recurring job for identification or grouping.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(1)]
    [System.ComponentModel.DataAnnotations.MaxLength(32)]
    System.String Tag { get; init; }

    /// <summary>
    /// Gets the amount of random jitter to add to the job interval.
    /// Jitter helps to spread out job executions and avoid thundering herd problems.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:00", "1.00:00:00")]
    System.TimeSpan? Jitter { get; init; }

    /// <summary>
    /// Gets the maximum backoff duration to wait before retrying after failures.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:00", "7.00:00:00")]
    System.TimeSpan BackoffCap { get; init; }

    /// <summary>
    /// Gets the maximum duration allowed for each job run.
    /// If the job exceeds this timeout, it may be cancelled.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:00", "1.00:00:00")]
    System.TimeSpan? ExecutionTimeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether the recurring job should be non-reentrant.
    /// Non-reentrant jobs prevent overlapping executions.
    /// </summary>
    [System.ComponentModel.DefaultValue(true)]
    System.Boolean NonReentrant { get; init; }

    /// <summary>
    /// Gets the maximum number of consecutive failures before applying backoff logic.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(0, System.Int32.MaxValue)]
    System.Int32 FailuresBeforeBackoff { get; init; }
}