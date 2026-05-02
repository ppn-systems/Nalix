// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Networking;

namespace Microsoft.Extensions.Logging;

/// <inheritdoc/>
public static partial class Log
{
    /// <inheritdoc/>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "{Message}{Suffix}")]
    public static partial void DataProcessingWarning(ILogger logger, string message, string suffix);

    /// <inheritdoc/>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Trace,
        Message = "{Message}{Suffix}")]
    public static partial void DataProcessingTrace(ILogger logger, string message, string suffix);

    /// <inheritdoc/>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "{Message}{Suffix}")]
    public static partial void DataProcessingError(ILogger logger, string message, string suffix);

    /// <inheritdoc/>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "{Message}{Suffix}")]
    [SuppressMessage("LoggingGenerator",
        "SYSLIB1006:Multiple logging methods cannot use the same event id within a class", Justification = "<Pending>")]
    public static partial void DataProcessingError(ILogger logger, Exception ex, string message, string suffix);
}

/// <summary>
/// Provides extension methods for throttled logging using connection attributes.
/// This prevents log flooding (DDoS Log) for events that happen frequently per-connection.
/// </summary>
public static class ThrottleLogExtensions
{
    private static readonly TimeSpan s_defaultWindow = TimeSpan.FromSeconds(10);

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
        if (!SHOULD_LOG(connection, key, out long suppressed))
        {
            return;
        }
        string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

        if (logger != null && logger.IsEnabled(LogLevel.Warning))
        {
            Log.DataProcessingWarning(logger, message, suffix);
        }
    }

    /// <summary>
    /// Logs an error message if the throttle window has passed for the given key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrottledError(this IConnection connection, ILogger? logger, string key, string message, Exception? ex = null)
    {
        if (!SHOULD_LOG(connection, key, out long suppressed))
        {
            return;
        }

        if (logger == null || !logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

        if (ex != null)
        {
            Log.DataProcessingError(logger, ex, message, suffix);
        }
        else
        {
            Log.DataProcessingError(logger, message, suffix);
        }
    }

    private static bool SHOULD_LOG(IConnection connection, string key, out long suppressed)
    {
        suppressed = 0;
        if (connection?.Attributes == null)
        {
            return true;
        }

        string attrKey = "sys.log." + key;
        bool created = false;
        if (!connection.Attributes.TryGetValue(attrKey, out object? val) || val is not LogThrottleState state)
        {
            state = new LogThrottleState { LastLogTicks = DateTime.UtcNow.Ticks };
            // ObjectMap's Add implementation is safe (it uses TryAdd internally and ignores if exists)
            connection.Attributes.Add(attrKey, state);

            // Re-fetch to handle race conditions where another thread added it first
            if (connection.Attributes.TryGetValue(attrKey, out val) && val is LogThrottleState existingState)
            {
                created = ReferenceEquals(existingState, state);
                state = existingState;
            }
            else
            {
                return true; // First time logging this key
            }
        }

        if (created)
        {
            return true;
        }

        long nowTicks = DateTime.UtcNow.Ticks;
        long lastTicks = Interlocked.Read(ref state.LastLogTicks);

        if (nowTicks - lastTicks >= s_defaultWindow.Ticks)
        {
            if (Interlocked.CompareExchange(ref state.LastLogTicks, nowTicks, lastTicks) == lastTicks)
            {
                suppressed = Interlocked.Exchange(ref state.SuppressedCount, 0);
                return true;
            }
        }

        _ = Interlocked.Increment(ref state.SuppressedCount);
        return false;
    }
}
