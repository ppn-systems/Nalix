// Copyright (c) 2025 PPN Corporation. All rights reserved.

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
public sealed class TaskManagerOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets a value indicating whether latency measurement is enabled.
    /// </summary>
    /// <remarks>
    /// When set to <see langword="true"/>, the system will collect and report
    /// latency information for diagnostic or performance monitoring purposes.
    /// </remarks>
    public System.Boolean IsEnableLatency { get; init; } = true;

    /// <summary>
    /// Enables or disables dynamic concurrency adjustment. Default is true.
    /// </summary>
    public System.Boolean DynamicAdjustmentEnabled { get; init; } = true;

    /// <summary>
    /// Maximum number of workers in the entire TaskManager (global limit). Defaults to 100.
    /// </summary>
    public System.Int32 MaxWorkers { get; init; } = 100;

    /// <summary>
    /// High CPU utilization threshold to reduce concurrency. Default is 80%.
    /// </summary>
    public System.Double ThresholdHighCpu { get; init; } = 80.0;

    /// <summary>
    /// Low CPU utilization threshold to increase concurrency. Default is 40%.
    /// </summary>
    public System.Double ThresholdLowCpu { get; init; } = 40.0;

    /// <summary>
    /// Time interval for monitoring system load (default 5 seconds).
    /// </summary>
    public System.TimeSpan ObservingInterval { get; init; } = System.TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the interval at which completed workers are cleaned up.
    /// Default is 30 seconds. Must be at least 1 second.
    /// </summary>
    /// <value>
    /// A <see cref="System.TimeSpan"/> representing the cleanup interval.
    /// </value>
    public System.TimeSpan CleanupInterval { get; init; } = System.TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the options and throws if any values are invalid.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when CleanupInterval is less than 1 second.</exception>
    public void Validate()
    {
        if (CleanupInterval < System.TimeSpan.FromSeconds(1))
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(CleanupInterval),
                CleanupInterval,
                "CleanupInterval must be at least 1 second");
        }
    }
}
