// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Options;

/// <summary>
/// Provides configuration options for the <see cref="Tasks.TaskManager"/>.
/// </summary>
public sealed class TaskManagerOptions
{
    /// <summary>
    /// Gets or sets the interval at which completed workers are cleaned up.
    /// Default is 30 seconds. Must be at least 1 second.
    /// </summary>
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
