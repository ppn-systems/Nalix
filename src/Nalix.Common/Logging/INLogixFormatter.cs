// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a contract for formatting Microsoft logger payloads into displayable or storable text.
/// </summary>
/// <remarks>
/// Implementations of this interface determine how log entries are represented as strings.
/// This may include adding timestamps, log levels, event IDs, or applying specific formatting styles (e.g., JSON, plain text).
/// </remarks>
public interface INLogixFormatter
{
    /// <summary>
    /// Formats the specified log payload into a string representation.
    /// </summary>
    /// <param name="timestampUtc">
    /// The UTC timestamp assigned to the log event.
    /// </param>
    /// <param name="logLevel">
    /// The Microsoft log level for the event.
    /// </param>
    /// <param name="eventId">
    /// The Microsoft event identifier for the event.
    /// </param>
    /// <param name="message">
    /// The rendered log message.
    /// </param>
    /// <param name="exception">
    /// The related exception, if any.
    /// </param>
    /// <returns>
    /// A string containing the formatted log message.
    /// </returns>
    string Format(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception);

    /// <summary>
    /// Formats the specified log payload into a string representation.
    /// </summary>    
    /// <param name="timestampUtc">
    /// The UTC timestamp assigned to the log event.
    /// </param>
    /// <param name="logLevel">
    /// The Microsoft log level for the event.
    /// </param>
    /// <param name="eventId">
    /// The Microsoft event identifier for the event.
    /// </param>
    /// <param name="message">
    /// The rendered log message.
    /// </param>
    /// <param name="exception">
    /// The related exception, if any.
    /// </param>
    /// <param name="sb">
    /// StringBuilder instance to which the formatted log message will be appended.
    /// </param>
    void Format(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception, StringBuilder sb);
}
