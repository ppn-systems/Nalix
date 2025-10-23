// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Tasks;

/// <summary>
/// Provides control and status information for a scheduled recurring job.
/// </summary>
public interface IRecurringHandle : System.IDisposable
{
    /// <summary>
    /// Gets the name of the recurring job.
    /// </summary>
    System.String Name { get; }

    /// <summary>
    /// Gets the total number of times the recurring job has run.
    /// </summary>
    System.Int64 TotalRuns { get; }

    /// <summary>
    /// Gets a value indicating whether the recurring job is currently running.
    /// </summary>
    System.Boolean IsRunning { get; }

    /// <summary>
    /// Gets the interval between each run of the recurring job.
    /// </summary>
    System.TimeSpan Interval { get; }

    /// <summary>
    /// Gets the options used to configure the recurring job.
    /// </summary>
    IRecurringOptions Options { get; }

    /// <summary>
    /// Gets the number of consecutive failures since the last successful run.
    /// </summary>
    System.Int32 ConsecutiveFailures { get; }

    /// <summary>
    /// Gets the UTC timestamp of the last run of the job, if any.
    /// </summary>
    System.DateTimeOffset? LastRunUtc { get; }

    /// <summary>
    /// Gets the scheduled UTC timestamp for the next run of the job, if any.
    /// </summary>
    System.DateTimeOffset? NextRunUtc { get; }
}