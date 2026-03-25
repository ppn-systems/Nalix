// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Concurrency;

/// <summary>
/// Provides control and status information for a scheduled recurring job.
/// </summary>
public interface IRecurringHandle : IDisposable
{
    /// <summary>
    /// Gets the name of the recurring job.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the total number of times the recurring job has run.
    /// </summary>
    long TotalRuns { get; }

    /// <summary>
    /// Gets a value indicating whether the recurring job is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the interval between each run of the recurring job.
    /// </summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Gets the options used to configure the recurring job.
    /// </summary>
    IRecurringOptions Options { get; }

    /// <summary>
    /// Gets the number of consecutive failures since the last successful run.
    /// </summary>
    int ConsecutiveFailures { get; }

    /// <summary>
    /// Gets the UTC timestamp of the last run of the job, if any.
    /// </summary>
    DateTimeOffset? LastRunUtc { get; }

    /// <summary>
    /// Gets the scheduled UTC timestamp for the next run of the job, if any.
    /// </summary>
    DateTimeOffset? NextRunUtc { get; }
}
