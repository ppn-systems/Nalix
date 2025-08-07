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
