// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Provides configuration options for the <see cref="Tasks.TaskManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class allows customization of TaskManager behavior such as cleanup intervals
/// for completed workers. All properties have sensible defaults.
/// </para>
/// <example>
/// <code>
/// var options = new TaskManagerOptions
/// {
///     CleanupInterval = TimeSpan.FromSeconds(60) // Cleanup every 60 seconds
/// };
/// var taskManager = new TaskManager(options);
/// </code>
/// </example>
/// </remarks>
[IniComment("Task manager configuration — controls concurrency, CPU thresholds, and cleanup behavior")]
public sealed class TaskManagerOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets a value indicating whether latency measurement is enabled.
    /// </summary>
    /// <remarks>
    /// When set to <see langword="true"/>, the system will collect and report
    /// latency information for diagnostic or performance monitoring purposes.
    /// </remarks>
    [IniComment("Enable latency measurement for diagnostic and performance monitoring")]
    public bool IsEnableLatency { get; init; } = true;

    /// <summary>
    /// Enables or disables dynamic concurrency adjustment. Default is true.
    /// </summary>
    [IniComment("Enable dynamic concurrency adjustment based on system load")]
    public bool DynamicAdjustmentEnabled { get; init; } = true;

    /// <summary>
    /// Maximum number of workers in the entire TaskManager (global limit). Defaults to 100.
    /// </summary>
    [IniComment("Global maximum number of concurrent workers (1–int.MaxValue)")]
    public int MaxWorkers { get; init; } = 100;

    /// <summary>
    /// High CPU utilization threshold to reduce concurrency. Default is 80%.
    /// </summary>
    [IniComment("CPU usage % above which concurrency is reduced (0–100)")]
    public double ThresholdHighCpu { get; init; } = 80.0;

    /// <summary>
    /// Low CPU utilization threshold to increase concurrency. Default is 40%.
    /// </summary>
    [IniComment("CPU usage % below which concurrency is increased (0–100)")]
    public double ThresholdLowCpu { get; init; } = 40.0;

    /// <summary>
    /// Time interval for monitoring system load (default 5 seconds).
    /// </summary>
    [IniComment("How often system load is sampled (e.g. 00:00:05 = 5 seconds)")]
    public TimeSpan ObservingInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the interval at which completed workers are cleaned up.
    /// Default is 30 seconds. Must be at least 1 second.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the cleanup interval.
    /// </value>
    [IniComment("How often completed workers are removed from memory (minimum 00:00:01)")]
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the options and throws if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when CleanupInterval is less than 1 second.</exception>
    public void Validate()
    {
        if (CleanupInterval < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(CleanupInterval),
                CleanupInterval,
                "CleanupInterval must be at least 1 second");
        }
    }
}
