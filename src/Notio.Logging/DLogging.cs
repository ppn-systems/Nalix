using Notio.Common.Logging;
using Notio.Common.Models;
using Notio.Logging.Core;
using Notio.Logging.Internal.File;
using Notio.Logging.Targets;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Notio.Logging;

/// <summary>
/// Provides a centralized logging interface for the Notio framework.
/// </summary>
public static class DLogging
{
    /// <summary>
    /// The global logging publisher used for distributing log messages to various targets.
    /// </summary>
    public static readonly ILoggingPublisher Publisher;

    /// <summary>
    /// Gets or sets the minimum logging level. Messages below this level will not be logged.
    /// </summary>
    public static LoggingLevel MinimumLevel { get; set; } = LoggingLevel.Trace;

    /// <summary>
    /// Initializes static members of the <see cref="DLogging"/> class.
    /// Configures the logging system with default targets and settings.
    /// </summary>
    static DLogging()
    {
        FileLoggerOptions fileLoggerOpts = new()
        {
            FormatLogFileName = (fname) =>
            {
                return Path.GetFileNameWithoutExtension(fname) + "_{0:yyyy}-{0:MM}-{0:dd}" + Path.GetExtension(fname);
            }
        };

        Publisher = new LoggingPublisher().AddTarget(new FileLoggingTarget(fileLoggerOpts));

#if DEBUG
        Publisher.AddTarget(new ConsoleLoggingTarget());
#endif
    }

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Debug(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Debug, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line number.</param>
    public static void Debug(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Debug, message, source?.FullName, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="extendedData">The exception.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Debug(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Debug, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Trace(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Trace, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line number.</param>
    public static void Trace(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Trace, message, source?.FullName, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Trace(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Trace, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Warn(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Warning, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line number.</param>
    public static void Warn(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Warning, message, source?.FullName, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Warn(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Warning, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Fatal(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Critical, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line number.</param>
    public static void Fatal(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Critical, message, source?.FullName, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Fatal(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Critical, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Info(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Information, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line number.</param>
    public static void Info(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Information, message, source?.FullName, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Info(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Information, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Error(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Error, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line number.</param>
    public static void Error(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Error, message, source?.FullName, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Error(
        this Exception ex,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LoggingLevel.Error, message, source, ex, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Log(
        this string message,
        string source,
        LoggingLevel messageType,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(messageType, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line number.</param>
    public static void Log(
        this string message,
        Type source,
        LoggingLevel messageType,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(messageType, message, source?.FullName, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="ex">The ex.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Log(
        this Exception ex,
        string? source = null,
        string? message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (ex is null)
            return;

        CreateLogEntry(LoggingLevel.Error, message ?? ex.Message, source ?? ex.Source, ex, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="ex">The ex.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line number. This is automatically populated.</param>
    public static void Log(
        this Exception ex,
        Type? source = null,
        string? message = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (ex is null)
            return;

        CreateLogEntry(LoggingLevel.Error, message ?? ex.Message, source?.FullName ?? ex.Source, ex, callerMemberName, callerFilePath, callerLineNumber);
    }

    private static void CreateLogEntry(
        LoggingLevel level,
        string message,
        string? sourceName,
        object? extendedData,
        string callerMemberName,
        string callerFilePath,
        int callerLineNumber)
    {
        if (!(level > MinimumLevel)) return;

        string fullMessage = BuildFullMessage(message, sourceName, extendedData, callerMemberName, callerFilePath, callerLineNumber);

        Publisher.Publish(new LoggingEntry(level, EventId.Empty, fullMessage, null));
    }

    private static string BuildFullMessage(
    string message,
    string? sourceName,
    object? extendedData,
    string callerMemberName,
    string callerFilePath,
    int callerLineNumber)
    {
        string extendedDataString = extendedData != null ? $"ExtendedData: {extendedData}" : "";

        return $@"
        [Message]: {message}
        [Source]: {sourceName ?? "Unknown"}
        [Caller]: {callerMemberName} in {callerFilePath} at line {callerLineNumber}
        {extendedDataString}".Trim();
    }
}
