// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents configuration options that control connection limiting
/// behavior per IP address.
/// </summary>
/// <remarks>
/// These options are designed to mitigate abuse, denial-of-service attempts,
/// and excessive resource consumption by restricting how many concurrent
/// and rapid connections a single IP address may establish.
/// <para>
/// Default values are optimized for security and performance and may be
/// adjusted depending on deployment environments such as NAT gateways,
/// reverse proxies, or carrier-grade networks.
/// </para>
/// </remarks>
public sealed class ConnectionLimitOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent connections
    /// allowed per IP address.
    /// </summary>
    /// <remarks>
    /// Default value is <c>10</c>.
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Suitable for most client-server workloads such as games, chat systems,
    /// and RESTful APIs.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// In environments where many users share the same IP address
    /// (e.g., NAT, proxies, ISPs), consider increasing this value
    /// to <c>100</c> or higher.
    /// </description>
    /// </item>
    /// </list>
    /// Recommended range: <c>1</c> to <c>10,000</c>.
    /// </remarks>
    public System.Int32 MaxConnectionsPerIpAddress { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of connection attempts
    /// allowed within the configured rate window.
    /// </summary>
    /// <remarks>
    /// This setting helps protect against burst-based connection floods.
    /// Default value is <c>10</c>.
    /// </remarks>
    public System.Int32 MaxConnectionsPerWindow { get; set; } = 10;

    /// <summary>
    /// Ban duration: Gets or sets the time window during which repeated connection attempts
    /// </summary>
    /// <remarks>
    /// All connection attempts from the same IP address are counted
    /// within this time window.
    /// Default value is <c>1 second</c>.
    /// </remarks>
    public System.TimeSpan BanDuration { get; set; } = System.TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the time window used to evaluate connection rate limits.
    /// </summary>
    /// <remarks>
    /// All connection attempts from the same IP address are counted
    /// within this time window.
    /// Default value is <c>1 second</c>.
    /// </remarks>
    public System.TimeSpan ConnectionRateWindow { get; set; } = System.TimeSpan.FromSeconds(5);

    /// <summary>
    /// DDoS mitigation: Gets or sets the time window during which repeated connection attempts.
    /// </summary>
    /// <remarks>
    /// All connection attempts from the same IP address are counted
    /// within this time window.
    /// Default value is <c>10 second</c>.
    /// </remarks>
    public System.TimeSpan DDoSLogSuppressWindow { get; set; } = System.TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets the interval at which cleanup operations are performed.
    /// </summary>
    /// <remarks>
    /// During cleanup, stale or expired connection tracking entries
    /// are removed to reduce memory usage.
    /// Default value is <c>60 seconds</c>.
    /// </remarks>
    public System.TimeSpan CleanupInterval { get; set; } = System.TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the duration after which an inactive connection
    /// is considered expired.
    /// </summary>
    /// <remarks>
    /// Connections that remain inactive beyond this threshold
    /// are eligible for cleanup.
    /// Default value is <c>5 minutes</c>.
    /// </remarks>
    public System.TimeSpan InactivityThreshold { get; set; } = System.TimeSpan.FromMinutes(5);
}
