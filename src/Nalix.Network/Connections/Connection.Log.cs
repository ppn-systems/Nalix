// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Time;

namespace Nalix.Network.Connections;

/// <summary>
/// Provides extension methods for throttled logging using connection attributes.
/// This prevents log flooding (DDoS Log) for events that happen frequently per-connection.
/// </summary>
public static class ConnectionLogExtensions
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
        if (SHOULD_LOG(connection, key, out long suppressed))
        {
            string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;
            logger?.Warn($"{message}{suffix}");
        }
    }

    /// <summary>
    /// Logs an error message if the throttle window has passed for the given key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrottledError(this IConnection connection, ILogger? logger, string key, string message, Exception? ex = null)
    {
        if (SHOULD_LOG(connection, key, out long suppressed))
        {
            string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;
            if (ex != null)
            {
                logger?.Error($"{message}{suffix}", ex);
            }
            else
            {
                logger?.Error($"{message}{suffix}");
            }
        }
    }

    /// <summary>
    /// Logs a trace message if the throttle window has passed for the given key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrottledTrace(this IConnection connection, ILogger? logger, string key, string message)
    {
        if (SHOULD_LOG(connection, key, out long suppressed))
        {
            string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;
            logger?.Trace($"{message}{suffix}");
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
        if (!connection.Attributes.TryGetValue(attrKey, out object? val) || val is not LogThrottleState state)
        {
            state = new LogThrottleState { LastLogTicks = Clock.NowUtc().Ticks };
            // ObjectMap's Add implementation is safe (it uses TryAdd internally and ignores if exists)
            connection.Attributes.Add(attrKey, state);

            // Re-fetch to handle race conditions where another thread added it first
            if (connection.Attributes.TryGetValue(attrKey, out val) && val is LogThrottleState existingState)
            {
                state = existingState;
            }
            else
            {
                return true; // First time logging this key
            }
        }

        long nowTicks = Clock.NowUtc().Ticks;
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
