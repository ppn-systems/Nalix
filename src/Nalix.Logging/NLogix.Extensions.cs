using Nalix.Common.Logging;

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Meta<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Meta($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs a trace-level message with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Trace<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Trace($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs a debug message with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Debug<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Debug<T>($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an informational message with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Info<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Info($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs a warning message with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Warn<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Warn($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an error message with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Error<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Error($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an exception as an error with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Error<T>(
        this ILogger logger, System.Exception ex, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Error($"[{typeof(T).Name}:{member}] {ex.Message}", ex, eventId);

    /// <summary>
    /// Logs a fatal error message with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fatal<T>(
        this ILogger logger, string message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Fatal($"[{typeof(T).Name}:{member}] {message}", eventId);

    /// <summary>
    /// Logs an exception as a fatal error with class and member context.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fatal<T>(
        this ILogger logger, System.Exception ex, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
        where T : class
        => logger.Fatal($"[{typeof(T).Name}:{member}] {ex.Message}", ex, eventId);
}
