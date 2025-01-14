// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;

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
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Meta(
        this System.String message,
        System.String? source = null,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(LogLevel.Meta, message, source, extendedData, callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Meta(
        this System.String message,
        System.Type source,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Meta, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="extendedData">The exception.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Meta(
        this System.Exception extendedData,
        System.String source,
        System.String message,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Meta, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

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
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Debug(
        this System.String message,
        System.String? source = null,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Debug, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Debug(
        this System.String message,
        System.Type source,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Debug, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a debug message to the console.
    /// </summary>
    /// <param name="extendedData">The exception.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Debug(
        this System.Exception extendedData,
        System.String source,
        System.String message,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Debug, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

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
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Trace(
        this System.String message,
        System.String? source = null,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Trace, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Trace(
        this System.String message,
        System.Type source,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Trace, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a trace message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Trace(
        this System.Exception extendedData,
        System.String source,
        System.String message,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Trace, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

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
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Warn(
        this System.String message,
        System.String? source = null,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Warning, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Warn(
        this System.String message,
        System.Type source,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Warning, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Warn(
        this System.Exception extendedData,
        System.String source,
        System.String message,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Warning, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

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
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fatal(
        this System.String message,
        System.String? source = null,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Critical, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fatal(
        this System.String message,
        System.Type source,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Critical, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs a warning message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fatal(
        this System.Exception extendedData,
        System.String source,
        System.String message,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Critical, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

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
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Info(
        this System.String message,
        System.String? source = null,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Information, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Info(
        this System.String message,
        System.Type source,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Information, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs an info message to the console.
    /// </summary>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Info(
        this System.Exception extendedData,
        System.String source,
        System.String message,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Information, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

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
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Error(
        this System.String message,
        System.String? source = null,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Error, message, source, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="source">The source.</param>
    /// <param name="extendedData">The extended data.</param>
    /// <param name="callerMemberName">Name of the caller member.</param>
    /// <param name="callerFilePath">The caller file path.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Error(
        this System.String message,
        System.Type source,
        System.Object? extendedData = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Error, message, source?.FullName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Logs an error message to the console's standard error.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="source">The source.</param>
    /// <param name="message">The message.</param>
    /// <param name="callerMemberName">Name of the caller member. This is automatically populated.</param>
    /// <param name="callerFilePath">The caller file path. This is automatically populated.</param>
    /// <param name="callerLineNumber">The caller line ProtocolType. This is automatically populated.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Error(
        this System.Exception ex,
        System.String source,
        System.String message,
        [System.Runtime.CompilerServices.CallerMemberName] System.String callerMemberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] System.String callerFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] System.Int32 callerLineNumber = 0)
        => PublishLogEntry(
            LogLevel.Error, message, source, ex,
            callerMemberName, callerFilePath, callerLineNumber);

    #endregion Error Methods
}
