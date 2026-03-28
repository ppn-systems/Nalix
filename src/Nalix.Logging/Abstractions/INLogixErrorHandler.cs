// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a contract for handling errors that occur during log processing.
/// </summary>
/// <remarks>
/// Implementations of this interface can be used to manage exceptions thrown by <see cref="INLogixTarget"/>
/// or other logging components when publishing log entries.
/// Typical use cases include retry mechanisms, writing errors to a fallback log,
/// or sending notifications to monitoring systems.
/// </remarks>
public interface INLogixErrorHandler
{
    /// <summary>
    /// Handles an error that occurred while processing a log entry.
    /// </summary>
    /// <param name="exception">
    /// The <see cref="Exception"/> that was thrown during logging.
    /// </param>
    /// <param name="timestampUtc">
    /// The UTC timestamp assigned to the original log event.
    /// </param>
    /// <param name="logLevel">
    /// The Microsoft log level of the original event.
    /// </param>
    /// <param name="eventId">
    /// The Microsoft event identifier of the original event.
    /// </param>
    /// <param name="message">
    /// The rendered message of the original event.
    /// </param>
    /// <param name="originalException">
    /// The exception that belonged to the original event, if any.
    /// </param>
    void HandleError(Exception exception, DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? originalException);
}
