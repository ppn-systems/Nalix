// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Configuration for object pool diagnostics and performance settings.
/// </summary>
[IniComment("Object pool configuration — controls diagnostics, lifetime tracking, and leak detection")]
public sealed partial class ObjectPoolOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Enables advanced diagnostics for object pools.
    /// When disabled, overhead is minimized for production performance.
    /// </summary>
    [IniComment("Enable advanced diagnostics (lifetime tracking, p95, suspicious object detection)")]
    public bool EnableDiagnostics { get; set; } = false;

    /// <summary>
    /// Enables lightweight traffic metrics for object pools (gets, returns, hit rate, traffic).
    /// Safe for production high-performance usage.
    /// </summary>
    [IniComment("Enable lightweight traffic metrics (safe for production)")]
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Captures stack traces when objects are rented from the pool.
    /// Extremely expensive; enable only for debugging leaks. Requires EnableDiagnostics=true.
    /// </summary>
    [IniComment("Capture allocation stack traces (expensive, use only for debugging leaks)")]
    public bool CaptureStackTraces { get; set; } = false;

    /// <summary>
    /// Threshold in seconds after which an outstanding object is considered "suspicious".
    /// </summary>
    [IniComment("Threshold in seconds to flag 'suspicious' objects in reports")]
    [ValueRange(0, 3600)]
    public int SuspiciousThresholdSeconds { get; set; } = 30;

    /// <summary>
    /// Enables GC-based leak detection using finalizers.
    /// When enabled, a sentinel is attached to rented objects to report if they are GC'd without being returned.
    /// </summary>
    [IniComment("Enable GC-based leak detection using sentinel finalizers")]
    public bool EnableLeakDetection { get; set; } = false;

    /// <summary>
    /// The number of recent lifetime samples to keep for p95 calculation.
    /// </summary>
    [IniComment("Number of recent samples to keep for percentile (p95) calculation")]
    [ValueRange(16, 1024)]
    public int LifetimeReservoirSize { get; set; } = 64;

    /// <summary>
    /// Enables automatic trimming of object pools (same as BufferPoolManager).
    /// </summary>
    [IniComment("Enable automatic memory trimming for object pools")]
    public bool EnableObjectTrimming { get; set; } = true;

    /// <summary>
    /// Interval between routine trim cycles (minutes).
    /// </summary>
    [IniComment("Trim interval in minutes")]
    [ValueRange(1, 60)]
    public int TrimIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Interval for deep (more aggressive) trim cycles.
    /// </summary>
    [IniComment("Deep trim interval in minutes")]
    [ValueRange(5, 120)]
    public int DeepTrimIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the base trim percentage used during normal maintenance cycles.
    /// Lower values keep more cached objects, while higher values free memory more aggressively.
    /// Default value is 25%.
    /// </summary>
    [IniComment("Base trim percentage for normal cycles (default ~25%)")]
    [ValueRange(10, 40)]
    public int BaseKeepPercentage { get; set; } = 25;

    /// <summary>
    /// Gets or sets the aggressive trim percentage used during deep cleanup cycles.
    /// This is typically applied when memory pressure is high or pools are oversized.
    /// Default value is 60%.
    /// </summary>
    [IniComment("Deep trim percentage (aggressive cycle)")]
    [ValueRange(45, 75)]
    public int DeepTrimPercentage { get; set; } = 60;

    /// <summary>
    /// Gets or sets the hit rate threshold that marks a pool as "hot".
    /// Hot pools are trimmed less aggressively to preserve performance.
    /// Default value is 85%.
    /// </summary>
    [IniComment("Hit rate threshold to be considered 'Hot' pool (light trim)")]
    [ValueRange(75.0, 98.0)]
    public double HotHitRateThreshold { get; set; } = 85.0;

    /// <summary>
    /// Gets or sets the minimum number of objects that should always remain in a pool.
    /// This prevents excessive trimming and reduces allocation spikes.
    /// Default value is 8.
    /// </summary>
    [IniComment("Minimum objects to keep in any pool (safety floor)")]
    [ValueRange(4, 64)]
    public int MinimumKeepObjects { get; set; } = 8;

    /// <summary>
    /// Gets or sets the default maximum capacity for newly created object pools.
    /// Default value is 8192. This applies to pools like BufferLease and ConnectionEventArgs.
    /// </summary>
    [IniComment("Default max capacity for any dynamically created pool (default 8192)")]
    [ValueRange(1024, 1_000_000)]
    public int DefaultMaxPoolSize { get; set; } = 8192;

    /// <summary>
    /// Number of instances to preallocate when a new object type pool is created.
    /// </summary>
    [IniComment("Objects to warm up for each newly created type pool (default 0 = lazy allocation)")]
    [ValueRange(0, 1_000_000)]
    public int DefaultPreallocate { get; set; } = 0;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="Abstractions.Exceptions.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate() => this.ValidateDataAnnotations();
}
