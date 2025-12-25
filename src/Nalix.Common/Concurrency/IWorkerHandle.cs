// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Identity;

namespace Nalix.Common.Concurrency;

/// <summary>
/// Provides status and control information for a background worker, including identity,
/// group, running state, progress, timing, and configuration options.
/// </summary>
public interface IWorkerHandle : IDisposable
{
    /// <summary>
    /// Gets the unique identifier of the worker.
    /// </summary>
    ISnowflake Id { get; }

    /// <summary>
    /// Gets the name of the worker.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the group to which the worker belongs.
    /// </summary>
    string Group { get; }

    /// <summary>
    /// Gets the total number of internal iterations performed by the worker.
    /// </summary>
    long TotalRuns { get; }

    /// <summary>
    /// Gets the current progress value, in user-defined units (e.g., bytes, messages).
    /// </summary>
    long Progress { get; }

    /// <summary>
    /// Gets the last note associated with the worker's progress.
    /// </summary>
    string? LastNote { get; }

    /// <summary>
    /// Gets the options used to configure the worker.
    /// </summary>
    IWorkerOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether the worker is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the UTC start time of the worker.
    /// </summary>
    DateTimeOffset StartedUtc { get; }

    /// <summary>
    /// Gets the UTC time of the worker's last heartbeat signal, if any.
    /// </summary>
    DateTimeOffset? LastHeartbeatUtc { get; }
}
