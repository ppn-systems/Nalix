using System;

namespace Nalix.Network.Security.Settings;

/// <summary>
/// Represents settings for connection limits, including maximum connections, cleanup interval, and inactivity threshold.
/// </summary>
public readonly struct ConnectionLimitSettings
{
    /// <summary>
    /// Gets the maximum Number of connections allowed.
    /// </summary>
    public int MaxConnections { get; }

    /// <summary>
    /// Gets the interval in milliseconds at which to perform cleanup operations.
    /// </summary>
    public int CleanupIntervalMs { get; }

    /// <summary>
    /// Gets the threshold in milliseconds for determining inactivity.
    /// </summary>
    public int InactivityThresholdMs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimitSettings"/> struct.
    /// </summary>
    /// <param name="maxConnections">The maximum Number of connections allowed.</param>
    /// <param name="cleanupIntervalMs">The interval in milliseconds for cleanup.</param>
    /// <param name="inactivityThresholdMs">The inactivity threshold in milliseconds.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any value is invalid.</exception>
    public ConnectionLimitSettings(int maxConnections, int cleanupIntervalMs, int inactivityThresholdMs)
    {
        if (maxConnections < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConnections), "MaxConnections must be at least 1.");
        if (cleanupIntervalMs < 1000)
            throw new ArgumentOutOfRangeException(nameof(cleanupIntervalMs), "CleanupIntervalMs must be at least 1000ms.");
        if (inactivityThresholdMs < 1000)
            throw new ArgumentOutOfRangeException(nameof(inactivityThresholdMs), "InactivityThresholdMs must be at least 1000ms.");

        MaxConnections = maxConnections;
        CleanupIntervalMs = cleanupIntervalMs;
        InactivityThresholdMs = inactivityThresholdMs;
    }

    /// <summary>
    /// Returns a string representation of the settings.
    /// </summary>
    public override string ToString() =>
        $"Connections: {MaxConnections}, CleanupInterval: {CleanupIntervalMs}ms, InactivityThreshold: {InactivityThresholdMs}ms";
}
