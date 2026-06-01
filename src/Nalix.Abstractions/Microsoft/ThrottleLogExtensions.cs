// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;

namespace Microsoft.Extensions.Logging;

/// <inheritdoc/>
public static partial class Log
{
    // ── Warning ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void DataProcessingWarning(ILogger logger, string message);

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message} (+{SuppressedCount} suppressed)")]
    public static partial void DataProcessingWarningSuppressed(ILogger logger, string message, long suppressedCount);

    // ── Trace ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Trace, Message = "{Message}")]
    public static partial void DataProcessingTrace(ILogger logger, string message);

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Trace, Message = "{Message} (+{SuppressedCount} suppressed)")]
    public static partial void DataProcessingTraceSuppressed(ILogger logger, string message, long suppressedCount);

    // ── Error ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void DataProcessingError(ILogger logger, string message);

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message} (+{SuppressedCount} suppressed)")]
    [SuppressMessage("LoggingGenerator",
        "SYSLIB1006:Multiple logging methods cannot use the same event id within a class", Justification = "<Pending>")]
    public static partial void DataProcessingErrorSuppressed(ILogger logger, string message, long suppressedCount);

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    [SuppressMessage("LoggingGenerator",
        "SYSLIB1006:Multiple logging methods cannot use the same event id within a class", Justification = "<Pending>")]
    public static partial void DataProcessingError(ILogger logger, Exception ex, string message);

    /// <inheritdoc/>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message} (+{SuppressedCount} suppressed)")]
    [SuppressMessage("LoggingGenerator",
        "SYSLIB1006:Multiple logging methods cannot use the same event id within a class", Justification = "<Pending>")]
    public static partial void DataProcessingErrorSuppressed(ILogger logger, Exception ex, string message, long suppressedCount);
}

/// <summary>
/// Provides extension methods for throttled logging using connection attributes.
/// This prevents log flooding (DDoS Log) for events that happen frequently per-connection.
/// </summary>
public static class ThrottleLogExtensions
{
    private const string AttrKeyPrefix = "sys.log.";

    private static readonly long s_defaultWindowTicks;
    private static readonly ConcurrentDictionary<string, string> s_attrKeyCache;

    static ThrottleLogExtensions()
    {
        s_attrKeyCache = new();
        s_defaultWindowTicks = (long)(TimeSpan.FromSeconds(20).TotalSeconds * Stopwatch.Frequency);
    }

    private sealed class LogThrottleState
    {
        public long LastLogTicks;
        public long SuppressedCount;
    }

    /// <summary>
    /// Logs a warning message if the throttle window has passed for the given key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrottledWarn(this IConnection connection, ILogger? logger, string key, string message)
    {
        if (logger == null || !logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        if (!ShouldLog(connection, key, out long suppressed))
        {
            return;
        }

        EmitWarning(logger, message, suppressed);
    }

    /// <summary>
    /// Logs an error message if the throttle window has passed for the given key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrottledError(this IConnection connection, ILogger? logger, string key, string message, Exception? ex = null)
    {
        if (logger == null || !logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        if (!ShouldLog(connection, key, out long suppressed))
        {
            return;
        }

        EmitError(logger, message, suppressed, ex);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ShouldLog(IConnection connection, string key, out long suppressed)
    {
        suppressed = 0;

        IObjectMap<string, object>? attrs = connection?.Attributes;
        if (attrs == null)
        {
            return true;
        }

        string attrKey = GetCachedAttrKey(key);

        if (!attrs.TryGetValue(attrKey, out object? val) || val is not LogThrottleState state)
        {
            LogThrottleState newState = new()
            {
                LastLogTicks = Stopwatch.GetTimestamp()
            };

            attrs.Add(attrKey, newState);

            return true;
        }

        long nowTicks = Stopwatch.GetTimestamp();
        long lastTicks = Interlocked.Read(ref state.LastLogTicks);

        if (nowTicks - lastTicks < s_defaultWindowTicks)
        {
            _ = Interlocked.Increment(ref state.SuppressedCount);
            return false;
        }

        if (Interlocked.CompareExchange(ref state.LastLogTicks, nowTicks, lastTicks) != lastTicks)
        {
            _ = Interlocked.Increment(ref state.SuppressedCount);
            return false;
        }

        suppressed = Interlocked.Exchange(ref state.SuppressedCount, 0);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitWarning(ILogger logger, string message, long suppressed)
    {
        if (suppressed > 0)
        {
            Log.DataProcessingWarningSuppressed(logger, message, suppressed);
        }
        else
        {
            Log.DataProcessingWarning(logger, message);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitError(ILogger logger, string message, long suppressed, Exception? ex)
    {
        if (ex == null)
        {
            if (suppressed > 0)
            {
                Log.DataProcessingErrorSuppressed(logger, message, suppressed);
            }
            else
            {
                Log.DataProcessingError(logger, message);
            }
        }
        else
        {
            if (suppressed > 0)
            {
                Log.DataProcessingErrorSuppressed(logger, ex, message, suppressed);
            }
            else
            {
                Log.DataProcessingError(logger, ex, message);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetCachedAttrKey(string key) => s_attrKeyCache.GetOrAdd(key, static k => string.Concat(AttrKeyPrefix, k));
}
