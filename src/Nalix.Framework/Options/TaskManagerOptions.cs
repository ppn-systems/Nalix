// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Abstractions;
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
    [System.ComponentModel.DataAnnotations.Range(0.0, 100.0, ErrorMessage = "ThresholdHighCpu must be between 0 and 100.")]
    public double ThresholdHighCpu { get; init; } = 80.0;

    /// <summary>
    /// Low CPU utilization threshold to increase concurrency. Default is 40%.
    /// </summary>
    [IniComment("CPU usage % below which concurrency is increased (0–100)")]
    [System.ComponentModel.DataAnnotations.Range(0.0, 100.0, ErrorMessage = "ThresholdLowCpu must be between 0 and 100.")]
    public double ThresholdLowCpu { get; init; } = 40.0;

    /// <summary>
    /// Time interval for monitoring system load (default 5 seconds).
    /// </summary>
    [IniComment("How often system load is sampled (e.g. 00:00:05 = 5 seconds)")]
    public TimeSpan ObservingInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Duration of the CPU measurement warmup period (default 60 seconds).
    /// </summary>
    [IniComment("Warmup period before CPU measurement baseline is established")]
    public TimeSpan CpuWarmupDuration { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Number of consecutive samples above/below threshold required to adjust concurrency.
    /// </summary>
    [IniComment("Consecutive samples required before adjusting concurrency (hysteresis)")]
    [System.ComponentModel.DataAnnotations.Range(1, 64)]
    public int AdjustmentStreakRequired { get; init; } = 3;

    /// <summary>
    /// Maximum duration for a busy-wait spin in recurring tasks (default 200 microseconds).
    /// </summary>
    [IniComment("Max duration for high-precision busy-wait spin (e.g. 00:00:00.0002)")]
    public TimeSpan BusyWaitThreshold { get; init; } = TimeSpan.FromTicks(2000); // 200 µs

    /// <summary>
    /// Maximum exponent for recurring task failure backoff (default 5 = 2^5 = 32x).
    /// </summary>
    [IniComment("Maximum power for exponential backoff (e.g. 5 = 2^5 = 32x)")]
    [System.ComponentModel.DataAnnotations.Range(0, 16)]
    public int BackoffMaxPower { get; init; } = 5;

    /// <summary>
    /// Base interval for recurring task failure backoff (default 1 second).
    /// </summary>
    [IniComment("Base interval for exponential backoff calculations")]
    public TimeSpan BackoffBaseInterval { get; init; } = TimeSpan.FromSeconds(1);

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
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);

        if (this.ThresholdHighCpu < this.ThresholdLowCpu)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException(
                $"{nameof(this.ThresholdHighCpu)} ({this.ThresholdHighCpu}) must be greater than or equal to {nameof(this.ThresholdLowCpu)} ({this.ThresholdLowCpu}).");
        }

        if (this.CleanupInterval < TimeSpan.FromSeconds(1))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("CleanupInterval must be at least 1 second.");
        }
        
        if (this.BusyWaitThreshold < TimeSpan.Zero)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("BusyWaitThreshold cannot be negative.");
        }

        if (this.BackoffBaseInterval < TimeSpan.FromMilliseconds(10))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("BackoffBaseInterval must be at least 10ms.");
        }

        if (this.CpuWarmupDuration < TimeSpan.Zero)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("CpuWarmupDuration cannot be negative.");
        }
    }
}
