// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Models;

namespace Nalix.Common.Logging.Abstractions;

/// <summary>
/// Defines a contract for handling errors that occur during log processing.
/// </summary>
/// <remarks>
/// Implementations of this interface can be used to manage exceptions thrown by <see cref="ILoggerTarget"/> 
/// or other logging components when publishing log entries.
/// Typical use cases include retry mechanisms, writing errors to a fallback log, 
/// or sending notifications to monitoring systems.
/// </remarks>
public interface ILoggerErrorHandler
{
    /// <summary>
    /// Handles an error that occurred while processing a log entry.
    /// </summary>
    /// <param name="exception">
    /// The <see cref="System.Exception"/> that was thrown during logging.
    /// </param>
    /// <param name="entry">
    /// The <see cref="LogEntry"/> that was being processed when the error occurred.
    /// </param>
    void HandleError(System.Exception exception, LogEntry entry);
}
