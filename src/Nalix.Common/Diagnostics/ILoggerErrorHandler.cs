// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Diagnostics;

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
    /// The <see cref="Exception"/> that was thrown during logging.
    /// </param>
    /// <param name="entry">
    /// The <see cref="LogEntry"/> that was being processed when the error occurred.
    /// </param>
    void HandleError(Exception exception, LogEntry entry);
}
