// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;

namespace Nalix.Shared.Extensions;

/// <summary>
/// A set of extension methods to support throttled logging.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Checks if logging is allowed for the provided throttle state.
    /// If allowed, resets the suppressed log count.
    /// </summary>
    /// <param name="state">The current throttle state.</param>
    /// <param name="currentTicks">The current time in ticks.</param>
    /// <param name="suppressed">The number of suppressed logs.</param>
    /// <returns>True if logging is allowed; otherwise false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean CanLog(ref this LogThrottleState state, System.Int64 currentTicks, out System.Int64 suppressed)
    {
        System.Int64 lastTicks = System.Threading.Interlocked.Read(ref state.LastLogTimeTicks);

        if (currentTicks - lastTicks < state.SuppressWindowTicks)
        {
            System.Threading.Interlocked.Increment(ref state.SuppressedCount);
            suppressed = 0;
            return false;
        }

        if (System.Threading.Interlocked.CompareExchange(ref state.LastLogTimeTicks, currentTicks, lastTicks) != lastTicks)
        {
            System.Threading.Interlocked.Increment(ref state.SuppressedCount);
            suppressed = 0;
            return false;
        }

        suppressed = System.Threading.Interlocked.Exchange(ref state.SuppressedCount, 0);
        return true;
    }

    /// <summary>
    /// Logs a throttled message. Suppresses logs within the configured time window.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="state">The throttle state to update.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="currentTicks">The current time in ticks.</param>
    public static void LogThrottled(this ILogger logger, ref LogThrottleState state, System.String message, System.Int64 currentTicks)
    {
        if (state.CanLog(currentTicks, out System.Int64 suppressed))
        {
            System.String suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : System.String.Empty;
            logger?.Warn($"{message}{suffix}");
        }
    }
}
