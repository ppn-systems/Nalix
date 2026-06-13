// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Runtime.Options;

/// <summary>
/// Configuration for the global concurrency gate and circuit breaker.
/// </summary>
[IniComment("Concurrency gate configuration — controls circuit breaker thresholds and entry cleanup")]
public sealed partial class ConcurrencyOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Rejection rate threshold (0.0-1.0) to trip the circuit breaker.
    /// Default 0.95 means 95% rejection rate trips it.
    /// </summary>
    [IniComment("Rejection rate (0.0–1.0) that trips the circuit breaker (default 0.95)")]
    [ValueRange(0.1, 1.0)]
    public double CircuitBreakerThreshold { get; set; } = 0.95;

    /// <summary>
    /// Minimum samples required before the circuit breaker can trip.
    /// </summary>
    [IniComment("Minimum samples before circuit breaker can trip (default 1000)")]
    [ValueRange(10, 1000000)]
    public int CircuitBreakerMinSamples { get; set; } = 1000;

    /// <summary>
    /// Duration in seconds to keep the circuit breaker open before attempting reset.
    /// </summary>
    [IniComment("Seconds to keep circuit breaker open before reset (default 60)")]
    [ValueRange(1, 3600)]
    public int CircuitBreakerResetAfterSeconds { get; set; } = 60;

    /// <summary>
    /// Minimum idle age before an opcode entry is considered for cleanup.
    /// </summary>
    [IniComment("Minimum idle age in minutes before entry cleanup (default 10)")]
    [ValueRange(1, 1440)]
    public int MinIdleAgeMinutes { get; set; } = 10;

    /// <summary>
    /// Interval in minutes between idle entry cleanup cycles.
    /// </summary>
    [IniComment("Cleanup cycle interval in minutes (default 1)")]
    [ValueRange(1, 60)]
    public int CleanupIntervalMinutes { get; set; } = 1;

    /// <summary>
    /// Default timeout in seconds for EnterAsync operations when queuing is enabled.
    /// </summary>
    [IniComment("Default timeout in seconds for EnterAsync queuing (default 20)")]
    [ValueRange(1, 300)]
    public int WaitTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    public void Validate() => this.ValidateDataAnnotations();
}
