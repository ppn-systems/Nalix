// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Diagnostics;

/// <summary>
/// Represents a single log entry recorded by the logging system.
/// </summary>
/// <param name="level">
/// The severity level of the log entry.
/// </param>
/// <param name="eventId">
/// The identifier associated with the event that produced the log entry.
/// </param>
/// <param name="message">
/// The message content describing the log event.
/// </param>
/// <param name="exception">
/// The exception related to the log entry, if any.
/// </param>
public readonly struct LogEntry(
    LogLevel level,
    EventId eventId,
    string message,
    Exception exception = null)
{
    /// <summary>
    /// Gets the severity level of the log entry.
    /// </summary>
    public readonly LogLevel LogLevel { get; } = level;

    /// <summary>
    /// Gets the identifier associated with the event that produced the log entry.
    /// </summary>
    public readonly EventId EventId { get; } = eventId;

    /// <summary>
    /// Gets the content of the log message.
    /// </summary>
    public readonly string Message { get; } = message;

    /// <summary>
    /// Gets the exception associated with the log entry, if any.
    /// </summary>
    public readonly Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the UTC timestamp indicating when the log entry was created.
    /// </summary>
    public readonly DateTime TimeStamp { get; } = DateTime.UtcNow;
}
