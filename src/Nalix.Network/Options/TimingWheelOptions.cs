// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration settings for idle connection timeout in the network layer.
/// Defines how long an inactive connection can stay open before being automatically closed.
/// </summary>
[IniComment("Timing wheel configuration — controls idle timeout detection for TCP and UDP connections")]
public sealed class TimingWheelOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the size of the timing wheel (number of buckets).
    /// Higher values reduce collisions but use a bit more memory.
    /// </summary>
    [IniComment("Number of timing wheel buckets — higher values reduce slot collisions (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int BucketCount { get; set; } = 512;

    /// <summary>
    /// Gets or sets the precision of the idle check tick in milliseconds.
    /// Lower values mean more frequent checks but slightly higher CPU usage.
    /// </summary>
    [IniComment("Tick interval in milliseconds — lower = more precise but higher CPU usage (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int TickDuration { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the idle timeout for TCP connections in milliseconds.
    /// </summary>
    [IniComment("TCP connection idle timeout in milliseconds before auto-close (default 60000 = 60s)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int IdleTimeoutMs { get; set; } = 60_000;

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
    }
}
