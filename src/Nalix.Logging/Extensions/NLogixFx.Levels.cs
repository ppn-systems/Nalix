using Nalix.Common.Logging;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Logging.Extensions;

public static partial class NLogixFx
{
    #region Meta Methods

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Meta(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(LogLevel.Meta, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);
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
    public static void Meta(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Meta, message, source?.FullName, extendedData,
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
    public static void Meta(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Meta, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    #endregion Meta Methods

    #region Debug Methods

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

    #endregion Debug Methods

    #region Trace Methods

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Trace(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Trace, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line Number.</param>
    public static void Trace(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry
            (LogLevel.Trace, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Trace(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Trace, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    #endregion Trace Methods

    #region Warning Methods

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Warn(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Warning, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line Number.</param>
    public static void Warn(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Warning, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Warn(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Warning, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    #endregion Warning Methods

    #region Fatal Methods

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Fatal(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Critical, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line Number.</param>
    public static void Fatal(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Critical, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Fatal(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Critical, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    #endregion Fatal Methods

    #region Info Methods

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Info(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Information, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line Number.</param>
    public static void Info(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Information, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Info(
        this Exception extendedData,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Information, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    #endregion Info Methods

    #region Error Methods

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Error(
        this string message,
        string? source = null,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Error, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line Number.</param>
    public static void Error(
        this string message,
        Type source,
        object? extendedData = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Error, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line Number. This is automatically populated.</param>
    public static void Error(
        this Exception ex,
        string source,
        string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        CreateLogEntry(
            LogLevel.Error, message, source, ex,
            callerMemberName, callerFilePath, callerLineNumber);
    }

    #endregion Error Methods
}
