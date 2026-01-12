// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Diagnostics;

/// <summary>
/// Defines a contract for a hierarchical logging system with multiple severity levels.
/// </summary>
/// <remarks>
/// Implementations decide how log entries are emitted (e.g., console, file, network) and
/// how formatting and error handling are applied. Overloads that accept an <see cref="EventId"/>
/// enable correlation and filtering across subsystems.
/// </remarks>
/// <example>
/// <code>
/// // Basic usage
/// ILogger logger = ...;
/// logger.Info("Server started.");
/// logger.Warn("Cache miss for key: {0}", key);
/// logger.ERROR(new InvalidOperationException("Invalid state"));
///
/// // With EventId correlation
/// EventId startup = new EventId(1001, "Startup");
/// logger.Info("Initializing modules...", startup);
/// </code>
/// </example>
public interface ILogger
{
    // =========================
    // Meta
    // =========================

    /// <summary>
    /// Writes a metadata-level log entry.
    /// </summary>
    /// <param name="message">
    /// The message text describing metadata such as configuration, environment, or version info.
    /// </param>
    void Meta(System.String message);

    /// <summary>
    /// Writes a metadata-level log entry using composite formatting.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format.</param>
    void Meta(System.String format, params System.Object[] args);

    /// <summary>
    /// Writes a metadata-level log entry with an optional event identifier.
    /// </summary>
    /// <param name="message">The message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Meta(System.String message, EventId? eventId = null);

    // =========================
    // Trace
    // =========================

    /// <summary>
    /// Writes a trace-level log entry for highly detailed diagnostics.
    /// </summary>
    /// <param name="message">The message text.</param>
    void Trace(System.String message);

    /// <summary>
    /// Writes a trace-level log entry using composite formatting.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format.</param>
    void Trace(System.String format, params System.Object[] args);

    /// <summary>
    /// Writes a trace-level log entry with an optional event identifier.
    /// </summary>
    /// <param name="message">The message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Trace(System.String message, EventId? eventId = null);

    // =========================
    // Debug
    // =========================

    /// <summary>
    /// Writes a debug-level log entry for development and troubleshooting.
    /// </summary>
    /// <param name="message">The message text.</param>
    void Debug(System.String message);

    /// <summary>
    /// Writes a debug-level log entry using composite formatting.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format.</param>
    void Debug(System.String format, params System.Object[] args);

    /// <summary>
    /// Writes a debug-level log entry with an optional event identifier.
    /// </summary>
    /// <param name="message">The message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Debug(System.String message, EventId? eventId = null);

    /// <summary>
    /// Writes a debug-level log entry with type and member context.
    /// </summary>
    /// <typeparam name="TClass">The class type providing contextual information.</typeparam>
    /// <param name="message">The message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    /// <param name="memberName">An optional member name where the log is emitted.</param>
    void Debug<TClass>(System.String message, EventId? eventId = null, System.String memberName = "")
        where TClass : class;

    // =========================
    // Information
    // =========================

    /// <summary>
    /// Writes an informational log entry describing normal application flow.
    /// </summary>
    /// <param name="message">The message text.</param>
    void Info(System.String message);

    /// <summary>
    /// Writes an informational log entry using composite formatting.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format.</param>
    void Info(System.String format, params System.Object[] args);

    /// <summary>
    /// Writes an informational log entry with an optional event identifier.
    /// </summary>
    /// <param name="message">The message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Info(System.String message, EventId? eventId = null);

    // =========================
    // Warning
    // =========================

    /// <summary>
    /// Writes a warning log entry for potentially harmful situations.
    /// </summary>
    /// <param name="message">The warning message text.</param>
    void Warn(System.String message);

    /// <summary>
    /// Writes a warning log entry using composite formatting.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format.</param>
    void Warn(System.String format, params System.Object[] args);

    /// <summary>
    /// Writes a warning log entry with an optional event identifier.
    /// </summary>
    /// <param name="message">The warning message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Warn(System.String message, EventId? eventId = null);

    // =========================
    // ERROR
    // =========================

    /// <summary>
    /// Writes an error log entry describing a handled failure.
    /// </summary>
    /// <param name="message">The error message text.</param>
    void Error(System.String message);

    /// <summary>
    /// Writes an error log entry with an optional event identifier.
    /// </summary>
    /// <param name="message">The error message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Error(System.String message, EventId? eventId = null);

    /// <summary>
    /// Writes an error log entry from an exception.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    void Error(System.Exception exception);

    /// <summary>
    /// Writes an error log entry using composite formatting.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format.</param>
    void Error(System.String format, params System.Object[] args);

    /// <summary>
    /// Writes an error log entry from an exception with an optional event identifier.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Error(System.Exception exception, EventId? eventId = null);

    /// <summary>
    /// Writes an error log entry with a custom message and exception details.
    /// </summary>
    /// <param name="message">The error message text.</param>
    /// <param name="exception">The associated exception.</param>
    void Error(System.String message, System.Exception exception);

    /// <summary>
    /// Writes an error log entry with a message, exception, and an optional event identifier.
    /// </summary>
    /// <param name="message">The error message text.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Error(System.String message, System.Exception exception, EventId? eventId = null);

    // =========================
    // Fatal / Critical
    // =========================

    /// <summary>
    /// Writes a critical log entry indicating a severe failure.
    /// </summary>
    /// <param name="message">The critical error message text.</param>
    void Fatal(System.String message);

    /// <summary>
    /// Writes a critical log entry using composite formatting.
    /// </summary>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format.</param>
    void Fatal(System.String format, params System.Object[] args);

    /// <summary>
    /// Writes a critical log entry with a custom message and exception details.
    /// </summary>
    /// <param name="message">The critical error message text.</param>
    /// <param name="exception">The associated exception.</param>
    void Fatal(System.String message, System.Exception exception);

    /// <summary>
    /// Writes a critical log entry with an optional event identifier.
    /// </summary>
    /// <param name="message">The critical error message text.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Fatal(System.String message, EventId? eventId = null);

    /// <summary>
    /// Writes a critical log entry with a message, exception, and an optional event identifier.
    /// </summary>
    /// <param name="message">The critical error message text.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">An optional event identifier for correlation and filtering.</param>
    void Fatal(System.String message, System.Exception exception, EventId? eventId = null);
}
