using Notio.Common.Logging;
using Notio.Logging.Engine;
using Notio.Logging.Options;
using Notio.Logging.Targets;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Notio.Logging.Extensions;

/// <summary>
/// Provides a centralized logging interface for the Notio framework.
/// </summary>
public static partial class NLogixFx
{
    #region Properties

    /// <summary>
    /// Gets or sets the minimum logging level. Messages below this level will not be logged.
    /// </summary>
    public static LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// The global logging publisher used for distributing log messages to various targets.
    /// </summary>
    public static readonly ILogDistributor Publisher;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes static members of the <see cref="NLogixFx"/> class.
    /// Configures the logging system with default targets and settings.
    /// </summary>
    static NLogixFx()
    {
        MinimumLevel = LogLevel.Trace;
        Publisher = new LogDistributor();

        FileLogOptions fileLoggerOpts = new()
        {
            FormatLogFileName = (fname) =>
            {
                return Path.GetFileNameWithoutExtension(fname) +
                       "_{0:yyyy}-{0:MM}-{0:dd}" + Path.GetExtension(fname);
            }
        };

        Publisher.AddTarget(new ConsoleLogTarget());
    }

    #endregion

    /// <summary>
    /// Logs the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Log(
        this string message,
        string source,
        LogLevel messageType,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            messageType, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
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
    /// <param name="callerLineNumber">The caller line Number.</param>
    public static void Log(
        this string message,
        Type source,
        LogLevel messageType,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            messageType, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="ex">The ex.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
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

        CreateLogEntry(
            LogLevel.Error, message ?? ex.Message, source ?? ex.Source,
            ex, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="ex">The ex.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
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

        CreateLogEntry(
            LogLevel.Error, message ?? ex.Message, source?.FullName ?? ex.Source,
            ex, callerMemberName, callerFilePath, callerLineNumber);
    }
}
