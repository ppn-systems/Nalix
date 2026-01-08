// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Options;

/// <summary>
/// Configuration options for the console logger.
/// </summary>
public sealed class BatchConsoleLogOptions
{
    /// <summary>
    /// Gets or sets the maximum number of log entries to batch before flushing to the console.
    /// </summary>
    public System.Int32 BatchSize { get; set; } = 32;

    /// <summary>
    /// Gets or sets the maximum number of log entries that can be queued. 0 means unlimited.
    /// </summary>
    public System.Int32 MaxQueueSize { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether adaptive flush is enabled.
    /// </summary>
    public System.Boolean AdaptiveFlush { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to block when the queue is full.
    /// </summary>
    public System.Boolean BlockWhenQueueFull { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether colored output is enabled.
    /// </summary>
    public System.Boolean EnableColors { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay between batch flushes.
    /// </summary>
    public System.TimeSpan BatchDelay { get; set; } = System.TimeSpan.FromMilliseconds(70);
}
