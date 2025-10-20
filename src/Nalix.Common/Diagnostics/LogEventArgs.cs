// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Diagnostics;

/// <summary>
/// Provides data for log-related events.
/// </summary>
/// <remarks>
/// This class encapsulates information about a logging event,
/// including the log level, message, and an optional exception.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="LogEventArgs"/> class.
/// </remarks>
/// <param name="level">
/// The severity level of the log entry.
/// </param>
/// <param name="message">
/// The log message describing the event.
/// </param>
/// <param name="exception">
/// The exception related to the log event, or <see langword="null"/>
/// if no exception is associated.
/// </param>
public sealed class LogEventArgs(
    LogLevel level,
    string message,
        Exception exception = null) : EventArgs
{
    /// <summary>
    /// Gets the severity level of the log entry.
    /// </summary>
    public LogLevel Level { get; } = level;

    /// <summary>
    /// Gets the log message associated with the event.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the exception associated with the log event, if any.
    /// </summary>
    /// <remarks>
    /// This property may be <see langword="null"/> when the log entry
    /// is not related to an exception.
    /// </remarks>
    public Exception Exception { get; } = exception;
}
