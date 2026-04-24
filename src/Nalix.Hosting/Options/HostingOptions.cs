// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Hosting.Options;

/// <summary>
/// Provides configuration options for Nalix hosting and bootstrapping.
/// </summary>
[IniComment("Hosting configuration — controls startup diagnostics, console behavior, and lifecycle settings")]
public sealed class HostingOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets a value indicating whether to disable clearing the console on startup.
    /// </summary>
    [IniComment("If true, the console will not be cleared during the startup sequence")]
    public bool DisableConsoleClear { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to disable the startup banner.
    /// </summary>
    [IniComment("If true, the Nalix startup banner and diagnostic info will not be printed")]
    public bool DisableStartupBanner { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum number of worker threads in the ThreadPool.
    /// </summary>
    [IniComment("Minimum worker threads for the ThreadPool (0 = system default, recommended: processor count * 2)")]
    public int MinWorkerThreads { get; set; } = 0;

    /// <summary>
    /// Gets or sets the minimum number of completion port threads in the ThreadPool.
    /// </summary>
    [IniComment("Minimum completion port threads for the ThreadPool (0 = system default)")]
    public int MinCompletionPortThreads { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to enable global unhandled exception handling and logging.
    /// </summary>
    [IniComment("If true, global unhandled exceptions will be caught and logged before process exit")]
    public bool EnableGlobalExceptionHandling { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable high-precision timers on Windows.
    /// </summary>
    [IniComment("If true, timeBeginPeriod(1) will be called on Windows to improve timer resolution (recommended for low-latency)")]
    public bool EnableHighPrecisionTimer { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public static void Validate()
    {
        // No validation needed for boolean flags
    }
}
