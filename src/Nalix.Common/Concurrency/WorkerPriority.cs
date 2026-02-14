// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Concurrency;

/// <summary>
/// Specifies the relative dispatch priority for queued workers.
/// </summary>
/// <remarks>
/// Higher values are scheduled ahead of lower values when workers are waiting
/// for a global execution slot inside <see cref="ITaskManager"/>.
/// </remarks>
public enum WorkerPriority : byte
{
    /// <summary>
    /// Lowest priority. Useful for maintenance and background cleanup work.
    /// </summary>
    LOW = 0,

    /// <summary>
    /// Default priority for normal worker traffic.
    /// </summary>
    NORMAL = 1,

    /// <summary>
    /// Elevated priority for latency-sensitive work.
    /// </summary>
    HIGH = 2,

    /// <summary>
    /// Highest priority for urgent workers that should run first when queued.
    /// </summary>
    URGENT = 3
}
