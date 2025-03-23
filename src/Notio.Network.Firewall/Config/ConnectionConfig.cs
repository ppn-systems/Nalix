using Notio.Common.Attributes;
using Notio.Network.Firewall.Connections;
using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Notio.Network.Firewall.Config;

/// <summary>
/// Configuration for limiting the number of concurrent connections per IP address.
/// This configuration helps manage and control the number of simultaneous connections from each IP.
/// </summary>
public sealed class ConnectionConfig : ConfiguredBinder
{
    // Pre-defined connection limits to avoid repetitive calculations
    private static readonly (int MaxConnections, int CleanupIntervalMs, int InactivityThresholdMs) LowSettings =
        (20, 30_000, 120_000);
    private static readonly (int MaxConnections, int CleanupIntervalMs, int InactivityThresholdMs) MediumSettings =
        (100, 60_000, 300_000);
    private static readonly (int MaxConnections, int CleanupIntervalMs, int InactivityThresholdMs) HighSettings =
        (500, 120_000, 600_000);
    private static readonly (int MaxConnections, int CleanupIntervalMs, int InactivityThresholdMs) UnlimitedSettings =
        (10000, 300_000, 1800_000);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionConfig"/> class with a specified connection limit.
    /// </summary>
    /// <param name="limit">The predefined connection limit to apply.</param>
    public ConnectionConfig(ConnectionLimit limit)
    {
        var settings = GetConnectionSettings(limit);

        MaxConnectionsPerIpAddress = settings.MaxConnections;
        CleanupIntervalMs = settings.CleanupIntervalMs;
        InactivityThresholdMs = settings.InactivityThresholdMs;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionConfig"/> class with a default connection limit of <see cref="ConnectionLimit.Medium"/>.
    /// </summary>
    public ConnectionConfig()
        : this(ConnectionLimit.Medium)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of connections allowed per IP address.
    /// </summary>
    /// <remarks>
    /// This value is limited to a range of 1 to 10000, where 100 is the default value.
    /// The configuration defines how many connections a single IP address can maintain simultaneously.
    /// </remarks>
    [Range(1, 10000)]
    public int MaxConnectionsPerIpAddress { get; set; } = 100;

    /// <summary>
    /// Gets or sets the interval in milliseconds between cleanup operations.
    /// </summary>
    [Range(1000, int.MaxValue)]
    public int CleanupIntervalMs { get; set; } = 60_000; // 1 minute

    /// <summary>
    /// Gets or sets the threshold in milliseconds after which an inactive connection entry is considered stale.
    /// </summary>
    [Range(1000, int.MaxValue)]
    public int InactivityThresholdMs { get; set; } = 300_000; // 5 minutes

    /// <summary>
    /// Gets the cleanup interval as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public TimeSpan CleanupInterval => TimeSpan.FromMilliseconds(CleanupIntervalMs);

    /// <summary>
    /// Gets the inactivity threshold as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public TimeSpan InactivityThreshold => TimeSpan.FromMilliseconds(InactivityThresholdMs);

    /// <summary>
    /// Gets predefined settings for a connection limit level.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int MaxConnections, int CleanupIntervalMs, int InactivityThresholdMs) GetConnectionSettings(ConnectionLimit limit)
        => limit switch
        {
            ConnectionLimit.Low => LowSettings,
            ConnectionLimit.Medium => MediumSettings,
            ConnectionLimit.High => HighSettings,
            ConnectionLimit.Unlimited => UnlimitedSettings,
            _ => MediumSettings
        };
}
