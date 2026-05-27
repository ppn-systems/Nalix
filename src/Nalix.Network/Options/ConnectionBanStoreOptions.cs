// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Configuration options for the persistent Banned IP store.
/// </summary>
[IniComment("Configuration for persisting banned IP addresses to disk")]
public sealed partial class ConnectionBanStoreOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether disk persistence is enabled.
    /// Default is true.
    /// </summary>
    [IniComment("Enable persisting banned IPs to disk (default true)")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the auto-save interval for flushing banned IPs to disk.
    /// Default is 5 minutes.
    /// </summary>
    [IniComment("Auto-save interval for flushing banned IPs to disk (00:01:00-01:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(TimeSpan), "00:01:00", "01:00:00", ErrorMessage = "AutoSaveInterval must be between 1 minute and 1 hour.")]
    public TimeSpan AutoSaveInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the file name used for storing banned IPs.
    /// Default is "banned_ips.bin".
    /// </summary>
    [IniComment("File name for storing banned IPs in the data directory (default banned_ips.bin)")]
    public string StoreFileName { get; set; } = "banned_ips.bin";

    /// <summary>
    /// Gets or sets the time window over which the ban count decays (reduces by 1).
    /// Default is 7 days.
    /// </summary>
    [IniComment("Time window over which the progressive ban count decays (01:00:00-30.00:00:00)")]
    [System.ComponentModel.DataAnnotations.Range(typeof(TimeSpan), "01:00:00", "30.00:00:00", ErrorMessage = "BanCountDecayWindow must be between 1 hour and 30 days.")]
    public TimeSpan BanCountDecayWindow { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum number of persisted bans to load from disk.
    /// Prevents excessive memory allocation if the file is corrupted.
    /// </summary>
    [IniComment("Maximum number of banned IPs to load from disk (10-1,000,000)")]
    [System.ComponentModel.DataAnnotations.Range(10, 1_000_000, ErrorMessage = "MaxPersistedBans must be between 10 and 1,000,000.")]
    public int MaxPersistedBans { get; set; } = 100_000;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    public void Validate()
    {
        this.ValidateDataAnnotations();
    }
}
