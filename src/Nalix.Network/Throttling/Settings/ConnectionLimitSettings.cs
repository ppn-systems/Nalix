using System;

namespace Nalix.Network.Throttling.Settings;

/// <summary>
/// Represents settings for connection limits, including maximum connections, cleanup interval, and inactivity threshold.
/// </summary>
public readonly struct ConnectionLimitSettings
{
    /// <summary>
    /// Gets the maximum ProtocolType of connections allowed.
    /// </summary>
    public Int32 MaxConnections { get; }

    /// <summary>
    /// Gets the interval in milliseconds at which to perform cleanup operations.
    /// </summary>
    public Int32 CleanupIntervalMs { get; }

    /// <summary>
    /// Gets the threshold in milliseconds for determining inactivity.
    /// </summary>
    public Int32 InactivityThresholdMs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionLimitSettings"/> struct.
    /// </summary>
    /// <param name="maxConnections">The maximum ProtocolType of connections allowed.</param>
    /// <param name="cleanupIntervalMs">The interval in milliseconds for cleanup.</param>
    /// <param name="inactivityThresholdMs">The inactivity threshold in milliseconds.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any value is invalid.</exception>
    public ConnectionLimitSettings(Int32 maxConnections, Int32 cleanupIntervalMs, Int32 inactivityThresholdMs)
    {
        if (maxConnections < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConnections), "MaxConnections must be at least 1.");
        }

        if (cleanupIntervalMs < 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(cleanupIntervalMs), "CleanupIntervalMs must be at least 1000ms.");
        }

        if (inactivityThresholdMs < 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(inactivityThresholdMs), "InactivityThresholdMs must be at least 1000ms.");
        }

        this.MaxConnections = maxConnections;
        this.CleanupIntervalMs = cleanupIntervalMs;
        this.InactivityThresholdMs = inactivityThresholdMs;
    }

    /// <summary>
    /// Returns a string representation of the settings.
    /// </summary>
    public override String ToString() =>
        $"Connections: {this.MaxConnections}, CleanupInterval: {this.CleanupIntervalMs}ms, InactivityThreshold: {this.InactivityThresholdMs}ms";
}
