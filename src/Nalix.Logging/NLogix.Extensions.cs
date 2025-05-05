using Nalix.Common.Logging;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Logging;

/// <summary>
/// Extension methods for <see cref="NLogix"/> that provide contextual logging
/// with automatic class and member name tagging.
/// </summary>
public static class NLogixExtensions
{
    /// <summary>
    /// Logs a meta-level message with class and member context.
    /// </summary>
    public static void Meta<T>(this NLogix logger, string message,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Meta($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs a trace-level message with class and member context.
    /// </summary>
    public static void Trace<T>(this NLogix logger, string message,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Trace($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs a debug message with class and member context.
    /// </summary>
    public static void Debug<T>(this NLogix logger, string message,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Debug<T>($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an informational message with class and member context.
    /// </summary>
    public static void Info<T>(this NLogix logger, string message,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Info($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs a warning message with class and member context.
    /// </summary>
    public static void Warn<T>(this NLogix logger, string message,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Warn($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an error message with class and member context.
    /// </summary>
    public static void Error<T>(this NLogix logger, string message,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Error($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an exception as an error with class and member context.
    /// </summary>
    public static void Error<T>(this NLogix logger, Exception ex,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Error($"[{typeof(T).Name}:{member}] {ex.Message}", ex, eventId);

    /// <summary>
    /// Logs a fatal error message with class and member context.
    /// </summary>
    public static void Fatal<T>(this NLogix logger, string message,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Fatal($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an exception as a fatal error with class and member context.
    /// </summary>
    public static void Fatal<T>(this NLogix logger, Exception ex,
        EventId? eventId = null,
        [CallerMemberName] string member = "")
        where T : class
        => logger.Fatal($"[{typeof(T).Name}:{member}] {ex.Message}", ex, eventId);
}
