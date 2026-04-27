// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration options for UDP datagram source rate limiting.
/// </summary>
[IniComment("UDP datagram guard — bounds source tracking and cleanup behavior")]
public sealed class DatagramGuardOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of IPv4 source windows tracked at once.
    /// </summary>
    [IniComment("Maximum IPv4 source windows tracked at once (default 65536)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "IPv4Windows must be between 1 and 10,000,000.")]
    public int IPv4Windows { get; set; } = 65_536;

    /// <summary>
    /// Gets or sets the maximum number of IPv6 source windows tracked at once.
    /// </summary>
    [IniComment("Maximum IPv6 source windows tracked at once (default 16384)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "IPv6Windows must be between 1 and 10,000,000.")]
    public int IPv6Windows { get; set; } = 16_384;

    /// <summary>
    /// Gets or sets the initial capacity for the IPv4 source window map.
    /// </summary>
    [IniComment("Initial IPv4 source map capacity (default 1024)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "IPv4Capacity must be between 1 and 10,000,000.")]
    public int IPv4Capacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the initial capacity for the IPv6 source window map.
    /// </summary>
    [IniComment("Initial IPv6 source map capacity (default 64)")]
    [System.ComponentModel.DataAnnotations.Range(1, 10_000_000, ErrorMessage = "IPv6Capacity must be between 1 and 10,000,000.")]
    public int IPv6Capacity { get; set; } = 64;

    /// <summary>
    /// Gets or sets how often stale source windows are purged.
    /// </summary>
    [IniComment("How often stale source windows are purged (00:00:01–01:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "CleanupInterval must be between 1 second and 1 hour.")]
    public System.TimeSpan CleanupInterval { get; set; } = System.TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long an inactive source window is retained before eviction.
    /// </summary>
    [IniComment("Idle time before a source window is evicted (00:00:01–01:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(System.TimeSpan), "00:00:01", "01:00:00", ErrorMessage = "IdleTimeout must be between 1 second and 1 hour.")]
    public System.TimeSpan IdleTimeout { get; set; } = System.TimeSpan.FromSeconds(10);

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
