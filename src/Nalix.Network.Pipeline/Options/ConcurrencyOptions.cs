// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Pipeline.Options;

/// <summary>
/// Configuration for the global concurrency gate and circuit breaker.
/// </summary>
[IniComment("Concurrency gate configuration — controls circuit breaker thresholds and entry cleanup")]
public sealed class ConcurrencyOptions : ConfigurationLoader
{
    /// <summary>
    /// Rejection rate threshold (0.0-1.0) to trip the circuit breaker.
    /// Default 0.95 means 95% rejection rate trips it.
    /// </summary>
    [IniComment("Rejection rate (0.0–1.0) that trips the circuit breaker (default 0.95)")]
    [Range(0.1, 1.0, ErrorMessage = "CircuitBreakerThreshold must be between 0.1 and 1.0.")]
    public double CircuitBreakerThreshold { get; set; } = 0.95;

    /// <summary>
    /// Minimum samples required before the circuit breaker can trip.
    /// </summary>
    [IniComment("Minimum samples before circuit breaker can trip (default 1000)")]
    [Range(10, 1000000, ErrorMessage = "CircuitBreakerMinSamples must be between 10 and 1,000,000.")]
    public int CircuitBreakerMinSamples { get; set; } = 1000;

    /// <summary>
    /// Duration in seconds to keep the circuit breaker open before attempting reset.
    /// </summary>
    [IniComment("Seconds to keep circuit breaker open before reset (default 60)")]
    [Range(1, 3600, ErrorMessage = "CircuitBreakerResetAfterSeconds must be between 1 and 3600.")]
    public int CircuitBreakerResetAfterSeconds { get; set; } = 60;

    /// <summary>
    /// Minimum idle age before an opcode entry is considered for cleanup.
    /// </summary>
    [IniComment("Minimum idle age in minutes before entry cleanup (default 10)")]
    [Range(1, 1440, ErrorMessage = "MinIdleAgeMinutes must be between 1 and 1440.")]
    public int MinIdleAgeMinutes { get; set; } = 10;

    /// <summary>
    /// Interval in minutes between idle entry cleanup cycles.
    /// </summary>
    [IniComment("Cleanup cycle interval in minutes (default 1)")]
    [Range(1, 60, ErrorMessage = "CleanupIntervalMinutes must be between 1 and 60.")]
    public int CleanupIntervalMinutes { get; set; } = 1;

    /// <summary>
    /// Default timeout in seconds for EnterAsync operations when queuing is enabled.
    /// </summary>
    [IniComment("Default timeout in seconds for EnterAsync queuing (default 20)")]
    [Range(1, 300, ErrorMessage = "WaitTimeoutSeconds must be between 1 and 300.")]
    public int WaitTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    public void Validate()
    {
        ValidationContext context = new(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
