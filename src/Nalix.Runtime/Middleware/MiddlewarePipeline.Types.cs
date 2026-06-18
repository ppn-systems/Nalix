// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Runtime.Middleware;

/// <summary>
/// Represents aggregated metrics for a middleware pipeline.
/// </summary>
public readonly struct PipelineMetrics
{
    /// <summary>
    /// Gets the number of packets currently being processed.
    /// </summary>
    public long ActiveExecutions { get; }

    /// <summary>
    /// Gets the total number of packets processed by the pipeline.
    /// </summary>
    public long TotalExecutions { get; }

    /// <summary>
    /// Gets the total execution time of the pipeline in ticks.
    /// </summary>
    public long TotalExecutionTicks { get; }

    /// <summary>
    /// Gets the highest execution time of a single packet in ticks.
    /// </summary>
    public long MaxExecutionTicks { get; }

    /// <summary>
    /// Gets the total number of non-fatal errors swallowed by the pipeline.
    /// </summary>
    public long TotalErrors { get; }

    /// <summary>
    /// Gets the average execution time per packet.
    /// </summary>
    public TimeSpan AverageExecutionTime => this.TotalExecutions == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(this.TotalExecutionTicks / this.TotalExecutions);

    internal PipelineMetrics(long activeExecutions, long totalExecutions, long totalExecutionTicks, long maxExecutionTicks, long totalErrors)
    {
        this.ActiveExecutions = activeExecutions;
        this.TotalExecutions = totalExecutions;
        this.TotalExecutionTicks = totalExecutionTicks;
        this.MaxExecutionTicks = maxExecutionTicks;
        this.TotalErrors = totalErrors;
    }
}

/// <summary>
/// Represents metrics for a specific middleware instance within the pipeline.
/// </summary>
public struct PerMiddlewareMetrics
{
    internal Type _middlewareType;

    internal long _totalErrors;
    internal long _totalExecutions;
    internal long _totalExecutionTicks;
    internal long _maxExecutionTicks;

    /// <summary>
    /// Gets the type of the middleware.
    /// </summary>
    public readonly Type MiddlewareType => _middlewareType;

    /// <summary>
    /// Gets the total number of non-fatal errors swallowed by this middleware.
    /// </summary>
    public readonly long TotalErrors => Interlocked.Read(ref Unsafe.AsRef(in _totalErrors));

    /// <summary>
    /// Gets the total number of packets processed by this middleware.
    /// </summary>
    public readonly long TotalExecutions => Interlocked.Read(ref Unsafe.AsRef(in _totalExecutions));

    /// <summary>
    /// Gets the total execution time spent in this middleware in ticks.
    /// </summary>
    public readonly long TotalExecutionTicks => Interlocked.Read(ref Unsafe.AsRef(in _totalExecutionTicks));

    /// <summary>
    /// Gets the highest execution time spent in this middleware in ticks.
    /// </summary>
    public readonly long MaxExecutionTicks => Interlocked.Read(ref Unsafe.AsRef(in _maxExecutionTicks));
}
