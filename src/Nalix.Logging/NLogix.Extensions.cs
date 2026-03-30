// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Nalix.Logging;

/// <summary>
/// Extension methods for <see cref="NLogix"/> that provide contextual logging
/// with automatic class and member name tagging.
/// </summary>
public static class NLogixExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatMessage<T>(string? message, string member) where T : class
        => $"[{typeof(T).Name}:{member}] {message ?? string.Empty}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LOG_CORE<T>(
        ILogger logger, LogLevel level, string message,
        Exception? exception, EventId eventId, string member) where T : class
    {
        if (logger is null || !logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(
            level,
            eventId,
            state: FormatMessage<T>(message, member),
            exception: exception,
            formatter: static (state, _) => state
        );
    }

    /// <summary>
    /// Logs a trace-level message with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Trace<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Trace, message, null, eventId ?? default, member);

    /// <summary>
    /// Logs a debug message with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Debug, message, null, eventId ?? default, member);

    /// <summary>
    /// Logs an informational message with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Info<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Information, message, null, eventId ?? default, member);

    /// <summary>
    /// Logs a warning message with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Warn<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Warning, message, null, eventId ?? default, member);

    #region Error

    /// <summary>
    /// Logs an error message with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Error, message, null, eventId ?? default, member);

    /// <summary>
    /// Logs an exception as an error with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Error<T>(
        this ILogger logger, Exception ex, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Error, ex?.Message ?? string.Empty, ex, eventId ?? default, member);

    #endregion Error

    #region Fatal

    /// <summary>
    /// Logs a fatal error message with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Critical<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Critical, message, null, eventId ?? default, member);

    /// <summary>
    /// Logs an exception as a fatal error with class and member context.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Critical<T>(
        this ILogger logger, Exception ex, EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => LOG_CORE<T>(logger, LogLevel.Critical, ex?.Message ?? string.Empty, ex, eventId ?? default, member);

    #endregion Fatal
}
