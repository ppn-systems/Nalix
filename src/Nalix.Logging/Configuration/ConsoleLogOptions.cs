// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Logging.Configuration;

/// <summary>
/// Configuration options for the console logger.
/// </summary>
[IniComment("Console logger configuration — controls batching, queue, and output behavior")]
public sealed class ConsoleLogOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the maximum number of log entries to batch before flushing to the console.
    /// </summary>
    [IniComment("Number of log entries to accumulate before flushing to the console")]
    public int BatchSize { get; set; } = 32;

    /// <summary>
    /// Gets or sets the maximum number of log entries that can be queued. 0 means unlimited.
    /// </summary>
    [IniComment("Maximum queued log entries (0 = unlimited)")]
    public int MaxQueueSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether adaptive flush is enabled.
    /// </summary>
    [IniComment("Dynamically adjust flush timing based on log volume")]
    public bool AdaptiveFlush { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to block when the queue is full.
    /// </summary>
    [IniComment("Block the caller instead of dropping entries when the queue is full")]
    public bool BlockWhenQueueFull { get; set; }

    /// <summary>
    /// Enables or disables flushing the console output after each batch is written.
    /// Enabling this may reduce performance but ensures that log messages are immediately visible in the console.
    /// </summary>
    [IniComment("Flush console output after each batch (reduces performance, improves visibility)")]
    public bool EnableFlush { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether colored output is enabled.
    /// </summary>
    [IniComment("Enable ANSI color coding for log level differentiation")]
    public bool EnableColors { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay between batch flushes.
    /// </summary>
    [IniComment("Delay between batch flushes (e.g. 00:00:00.0700000 = 70 ms)")]
    public System.TimeSpan BatchDelay { get; set; } = System.TimeSpan.FromMilliseconds(70);
}
