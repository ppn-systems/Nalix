// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Configuration for buffer settings with validation.
/// </summary>
[IniComment("Buffer pool configuration")]
public sealed partial class BufferOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Enables GC-based leak detection using finalizers.
    /// When enabled, a sentinel is attached to rented objects to report if they are GC'd without being returned.
    /// </summary>
    [IniComment("Enable GC-based leak detection using sentinel finalizers")]
    public bool EnableBufferLeakDetection { get; set; } = false;

    /// <summary>
    /// Enable capturing stack traces for buffer leaks.
    /// Capturing stack trace is extremely expensive and should be disabled
    /// during high-concurrency benchmarks.
    /// </summary>
    [IniComment("Enable expensive stack trace capture for buffer leak detection")]
    public bool EnableBufferLeakStackTrace { get; set; } = false;

    /// <summary>
    /// Threshold in seconds after which an outstanding object is considered "suspicious".
    /// </summary>
    [IniComment("Threshold in seconds to flag 'suspicious' objects in reports")]
    [ValueRange(0, 3600)]
    public int SuspiciousThresholdSeconds { get; set; } = 30;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate() => this.ValidateDataAnnotations();
}
