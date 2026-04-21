// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Configuration for object pool diagnostics and performance settings.
/// </summary>
[IniComment("Object pool configuration — controls diagnostics, lifetime tracking, and leak detection")]
public sealed class ObjectPoolConfig : ConfigurationLoader
{
    /// <summary>
    /// Enables advanced diagnostics for object pools.
    /// When disabled, overhead is minimized for production performance.
    /// </summary>
    [IniComment("Enable advanced diagnostics (lifetime tracking, p95, suspicious object detection)")]
    public bool EnableDiagnostics { get; set; } = false;

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
    [Range(1, 3600, ErrorMessage = "SuspiciousThresholdSeconds must be between 1 and 3600.")]
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
    [Range(16, 1024, ErrorMessage = "LifetimeReservoirSize must be between 16 and 1024.")]
    public int LifetimeReservoirSize { get; set; } = 64;
}
