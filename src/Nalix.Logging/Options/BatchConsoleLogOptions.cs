// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Options;

/// <summary>
/// Options for configuring <see cref="Nalix.Logging.Sinks.BatchConsoleLogTarget"/>.
/// </summary>
public sealed class BatchConsoleLogOptions
{
    /// <summary>
    /// Gets or sets the flush interval for the in-memory buffer.
    /// </summary>
    public System.TimeSpan FlushInterval { get; set; } = System.TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum number of buffered entries before triggering an auto flush.
    /// </summary>
    public System.Int32 MaxBufferSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether a flush should be triggered automatically when the buffer is full.
    /// </summary>
    public System.Boolean AutoFlushOnFull { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use colored console output (will be passed to the underlying console target/formatter).
    /// </summary>
    public System.Boolean EnableColors { get; set; } = true;
}
