// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Tasks.Options;

namespace Nalix.Common.Tasks;

/// <summary>
/// Provides status and control information for a background worker, including identity,
/// group, running state, progress, timing, and configuration options.
/// </summary>
public interface IWorkerHandle : System.IDisposable
{
    /// <summary>
    /// Gets the unique identifier of the worker.
    /// </summary>
    IIdentifier Id { get; }

    /// <summary>
    /// Gets the name of the worker.
    /// </summary>
    System.String Name { get; }

    /// <summary>
    /// Gets the group to which the worker belongs.
    /// </summary>
    System.String Group { get; }

    /// <summary>
    /// Gets the total number of internal iterations performed by the worker.
    /// </summary>
    System.Int64 TotalRuns { get; }

    /// <summary>
    /// Gets the current progress value, in user-defined units (e.g., bytes, messages).
    /// </summary>
    System.Int64 Progress { get; }

    /// <summary>
    /// Gets the last note associated with the worker's progress.
    /// </summary>
    System.String LastNote { get; }

    /// <summary>
    /// Gets the options used to configure the worker.
    /// </summary>
    IWorkerOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether the worker is currently running.
    /// </summary>
    System.Boolean IsRunning { get; }

    /// <summary>
    /// Gets the UTC start time of the worker.
    /// </summary>
    System.DateTimeOffset StartedUtc { get; }

    /// <summary>
    /// Gets the UTC time of the worker's last heartbeat signal, if any.
    /// </summary>
    System.DateTimeOffset? LastHeartbeatUtc { get; }
}