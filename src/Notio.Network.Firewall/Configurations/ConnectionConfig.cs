using Notio.Common.Attributes;
using Notio.Network.Security.Enums;
using Notio.Network.Security.Metadata;
using Notio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Notio.Network.Security.Configurations;

/// <summary>
/// Configuration for limiting the number of concurrent connections per IP address.
/// This configuration helps manage and control the number of simultaneous connections from each IP.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConnectionConfig"/> class with the specified connection limit settings.
/// </remarks>
/// <param name="settings">The connection limit settings to apply.</param>
public sealed class ConnectionConfig(ConnectionLimitSettings settings) : ConfigurationBinder
{
    // Pre-defined connection limits to avoid repetitive calculations
    private static readonly ConnectionLimitSettings LowSettings = new(20, 30_000, 120_000);
    private static readonly ConnectionLimitSettings MediumSettings = new(100, 60_000, 300_000);
    private static readonly ConnectionLimitSettings HighSettings = new(500, 120_000, 600_000);
    private static readonly ConnectionLimitSettings UnlimitedSettings = new(10_000, 300_000, 1_800_000);

    private static readonly Dictionary<ConnectionLimitType, ConnectionLimitSettings> SettingsMap = new()
    {
        { ConnectionLimitType.Low, LowSettings },
        { ConnectionLimitType.Medium, MediumSettings },
        { ConnectionLimitType.High, HighSettings },
        { ConnectionLimitType.Unlimited, UnlimitedSettings }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionConfig"/> class with a specified connection limit.
    /// </summary>
    /// <param name="limit">The predefined connection limit to apply.</param>
    public ConnectionConfig(ConnectionLimitType limit)
        : this(GetConnectionSettings(limit))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionConfig"/> class with a default connection limit of <see cref="ConnectionLimitType.Medium"/>.
    /// </summary>
    public ConnectionConfig()
        : this(ConnectionLimitType.Medium)
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
    public int MaxConnectionsPerIpAddress { get; set; } = settings.MaxConnections;

    /// <summary>
    /// Gets or sets the interval in milliseconds between cleanup operations.
    /// </summary>
    [Range(1000, int.MaxValue)]
    public int CleanupIntervalMs { get; set; } = settings.CleanupIntervalMs;

    /// <summary>
    /// Gets or sets the threshold in milliseconds after which an inactive connection entry is considered stale.
    /// </summary>
    [Range(1000, int.MaxValue)]
    public int InactivityThresholdMs { get; set; } = settings.InactivityThresholdMs;

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
    private static ConnectionLimitSettings GetConnectionSettings(ConnectionLimitType limit)
        => SettingsMap.TryGetValue(limit, out var settings) ? settings : MediumSettings;
}
