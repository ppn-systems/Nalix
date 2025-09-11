// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Provides configuration options for limiting the number of concurrent
/// connections allowed per IP address.
/// </summary>
/// <remarks>
/// This configuration helps prevent abuse and excessive resource usage by
/// restricting how many simultaneous connections a single IP can establish.
/// Default values are chosen for security and efficiency, but can be adjusted
/// based on deployment scenarios (e.g., NAT, proxies, or carrier-grade networks).
/// </remarks>
public sealed class ConnectionLimitOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of connections allowed per IP address.
    /// </summary>
    /// <remarks>
    /// Default is 50.
    /// - Suitable for most client-to-server scenarios (e.g., games, chat, APIs).
    /// - In environments where multiple users share the same IP
    ///   (e.g., NAT, proxies, ISPs), consider increasing this value
    ///   to 100 or higher.
    /// - Range: 1 to 10,000.
    /// </remarks>
    public System.Int32 MaxConnectionsPerIpAddress { get; set; } = 50;

    /// <summary>
    /// Gets or sets the interval in milliseconds between cleanup operations.
    /// </summary>
    /// <remarks>
    /// During cleanup, stale or inactive connection entries are removed.
    /// Default is 60,000 ms (1 minute).
    /// </remarks>
    public System.Int32 CleanupIntervalMs { get; set; } = 60_000;

    /// <summary>
    /// Gets or sets the inactivity threshold in milliseconds.
    /// </summary>
    /// <remarks>
    /// A connection entry is considered stale if it has been inactive longer
    /// than this threshold.
    /// Default is 300,000 ms (5 minutes).
    /// </remarks>
    public System.Int32 InactivityThresholdMs { get; set; } = 300_000;

    /// <summary>
    /// Gets the cleanup interval as a <see cref="System.TimeSpan"/>.
    /// </summary>
    [ConfiguredIgnore]
    public System.TimeSpan CleanupInterval => System.TimeSpan.FromMilliseconds(this.CleanupIntervalMs);

    /// <summary>
    /// Gets the inactivity threshold as a <see cref="System.TimeSpan"/>.
    /// </summary>
    [ConfiguredIgnore]
    public System.TimeSpan InactivityThreshold => System.TimeSpan.FromMilliseconds(this.InactivityThresholdMs);
}