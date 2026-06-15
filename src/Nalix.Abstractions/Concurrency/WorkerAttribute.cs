// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Concurrency;

/// <summary>
/// Annotates an <see cref="IWorker"/> with metadata used by the task manager
/// to automatically configure name, group, priority, and options when scheduling.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class WorkerAttribute : Attribute
{
    /// <summary>
    /// Worker name used for identification in TaskManager reports.
    /// Example: "session.cleanup", "log.console.worker"
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Worker group for bulk operations (CancelGroup, WaitGroupAsync).
    /// Example: "cleanup", "log", "net"
    /// </summary>
    public string Group { get; }

    /// <summary>
    /// Short tag for filtering and diagnostics.
    /// Default: same as Group.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Worker priority in the scheduling queue.
    /// Default: NORMAL (0).
    /// Maps to WorkerPriority enum.
    /// </summary>
    public int Priority { get; init; } // WorkerPriority.NORMAL

    /// <summary>
    /// Snowflake ID type for the worker handle.
    /// Default: SnowflakeType.System (1).
    /// </summary>
    public int IdType { get; init; } = 1; // SnowflakeType.System

    /// <summary>
    /// How long (ms) to retain the worker state after completion.
    /// Default: 0 (immediate cleanup).
    /// </summary>
    public int RetainForMs { get; init; }

    /// <summary>
    /// Maximum concurrent workers allowed in this group.
    /// Default: 0 (unlimited).
    /// </summary>
    public int GroupConcurrencyLimit { get; init; }

    /// <summary>
    /// Whether the worker is enabled by default.
    /// Set to false to register the attribute but not auto-schedule.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Preferred CPU core index (0-based) for thread affinity on dedicated threads.
    /// Default: -1 (no affinity). Only effective when the worker has a dedicated thread.
    /// </summary>
    public int ProcessorAffinity { get; init; } = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerAttribute"/> class.
    /// </summary>
    /// <param name="name">The worker name.</param>
    /// <param name="group">The worker group.</param>
    public WorkerAttribute(string name, string group)
    {
        this.Name = name;
        this.Group = group;
    }
}
