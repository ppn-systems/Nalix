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
    /// Recommended range: <c>1</c> to <c>10,000</c>.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, 10_000, ErrorMessage = "MaxConnectionsPerIpAddress must be between 1 and 10,000.")]
    public System.Int32 MaxConnectionsPerIpAddress { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of connection attempts
    /// allowed within the configured rate window.
    /// </summary>
    /// <remarks>
    /// Default value is <c>10</c>.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "MaxConnectionsPerWindow must be between 1 and 10,000.")]
    public System.Int32 MaxConnectionsPerWindow { get; set; } = 10;

    /// <summary>
    /// Ban duration: Gets or sets the time window during which repeated connection attempts
    /// </summary>
    /// <remarks>
    /// All connection attempts from the same IP address are counted
    /// within this time window.
    /// Default value is <c>5 minutes</c>.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "1.00:00:00", ErrorMessage = "BanDuration must be at least 1 second and at most 1 day.")]
    public System.TimeSpan BanDuration { get; set; } = System.TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the time window used to evaluate connection rate limits.
    /// </summary>
    /// <remarks>
    /// Default value is <c>5 seconds</c>.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "00:10:00", ErrorMessage = "ConnectionRateWindow must be between 1 second and 10 minutes.")]
    public System.TimeSpan ConnectionRateWindow { get; set; } = System.TimeSpan.FromSeconds(5);

    /// <summary>
    /// DDoS mitigation: Gets or sets the time window during which repeated connection attempts.
    /// </summary>
    /// <remarks>
    /// Default value is <c>20 seconds</c>.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "DDoSLogSuppressWindow must be between 1 second and 1 hour.")]
    public System.TimeSpan DDoSLogSuppressWindow { get; set; } = System.TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets the interval at which cleanup operations are performed.
    /// </summary>
    /// <remarks>
    /// Default value is <c>60 seconds</c>.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "CleanupInterval must be between 1 second and 1 hour.")]
    public System.TimeSpan CleanupInterval { get; set; } = System.TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the duration after which an inactive connection
    /// is considered expired.
    /// </summary>
    /// <remarks>
    /// Default value is <c>5 minutes</c>.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "1.00:00:00", ErrorMessage = "InactivityThreshold must be at least 1 second and at most 1 day.")]
    public System.TimeSpan InactivityThreshold { get; set; } = System.TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);

        if (MaxConnectionsPerIpAddress < 1)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxConnectionsPerIpAddress must be at least 1.");
        }
        if (MaxConnectionsPerWindow < 1)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("MaxConnectionsPerWindow must be at least 1.");
        }
        if (ConnectionRateWindow.TotalSeconds > 600)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("ConnectionRateWindow should not exceed 10 minutes.");
        }
        if (BanDuration < System.TimeSpan.FromSeconds(1))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("BanDuration must be at least 1 second.");
        }
        if (BanDuration > System.TimeSpan.FromDays(1))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("BanDuration is unrealistically long (over 1 day).");
        }
        if (CleanupInterval < System.TimeSpan.FromSeconds(1) || CleanupInterval > System.TimeSpan.FromHours(1))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("CleanupInterval must be between 1 second and 1 hour.");
        }
        if (InactivityThreshold <= System.TimeSpan.Zero)
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("InactivityThreshold must be a positive TimeSpan.");
        }
        if (InactivityThreshold < System.TimeSpan.FromSeconds(1))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("InactivityThreshold must be at least 1 second.");
        }
    }
}