using Notio.Common.Logging;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Logging.Extensions;

public static partial class NLogixFx
{
    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Debug(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LogLevel.Debug, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line Number.</param>
    public static void Debug(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Debug, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="extendedData">The exception.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Debug(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Debug, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }
}
