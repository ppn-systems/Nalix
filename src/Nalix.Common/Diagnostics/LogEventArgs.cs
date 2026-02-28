// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace Nalix.Common.Diagnostics;

/// <summary>
/// Provides data for log-related events.
/// </summary>
/// <remarks>
/// This class encapsulates information about a logging event,
/// including the log level, message, and an optional exception.
/// </remarks>
public sealed class LogEventArgs : System.EventArgs
{
    /// <summary>
    /// Gets the severity level of the log entry.
    /// </summary>
    public LogLevel Level { get; }

    /// <summary>
    /// Gets the log message associated with the event.
    /// </summary>
    public System.String Message { get; }

    /// <summary>
    /// Gets the exception associated with the log event, if any.
    /// </summary>
    /// <remarks>
    /// This property may be <see langword="null"/> when the log entry
    /// is not related to an exception.
    /// </remarks>
    public System.Exception Exception { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEventArgs"/> class.
    /// </summary>
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
    public LogEventArgs(
        LogLevel level,
        System.String message,
        System.Exception exception = null)
    {
        Level = level;
        Message = message;
        Exception = exception;
    }
}