// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Network.RateLimiting;

namespace Nalix.Network.Internal.Security;

/// <summary>
/// Gatekeeper for throttled logging. Prevents log flooding by suppressing messages within a time window.
/// </summary>
internal static class ThrottledLogGate
{
    /// <summary>
    /// Generic throttled logger slot acquire.
    /// Returns true if the log slot is acquired (wins the CAS), false if suppressed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryAcquire(ref long lastLogTicks, ref long suppressedCount, long nowTicks, long windowTicks, out long suppressed)
    {
        long lastTicks = Interlocked.Read(ref lastLogTicks);

        if (nowTicks - lastTicks >= windowTicks)
        {
            if (Interlocked.CompareExchange(ref lastLogTicks, nowTicks, lastTicks) == lastTicks)
            {
                suppressed = Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        _ = Interlocked.Increment(ref suppressedCount);

        long newLastTicks = Interlocked.Read(ref lastLogTicks);
        if (nowTicks - newLastTicks >= windowTicks)
        {
            if (Interlocked.CompareExchange(ref lastLogTicks, nowTicks, newLastTicks) == newLastTicks)
            {
                suppressed = Interlocked.Exchange(ref suppressedCount, 0);
                return true;
            }
        }

        suppressed = 0;
        return false;
    }

    /// <summary>
    /// Logs a DDoS detection warning with throttled behavior.
    /// </summary>
    public static void LogDDoSDetected(ILogger? logger, ConnectionGuard.ConnectionLimitEntry entry, string address, long nowTicks, long windowTicks)
    {
        long lastTicks = Interlocked.Read(ref entry.LastDDoSLogTicks);

        if (nowTicks - lastTicks < windowTicks)
        {
            _ = Interlocked.Increment(ref entry.SuppressedDDoSCount);
            return;
        }

        if (Interlocked.CompareExchange(ref entry.LastDDoSLogTicks, nowTicks, lastTicks) != lastTicks)
        {
            _ = Interlocked.Increment(ref entry.SuppressedDDoSCount);
            return;
        }

        long suppressed = Interlocked.Exchange(ref entry.SuppressedDDoSCount, 0);

        if (logger != null && logger.IsEnabled(LogLevel.Warning))
        {
            if (suppressed > 0)
            {
                logger.LogWarning("[NW.ConnectionGuard] DDoS-detected ip={Address} (+{Suppressed} suppressed-in-last={TimeSpanFromTickswindowTicksTotalSeconds}s)", address, suppressed, TimeSpan.FromTicks(windowTicks).TotalSeconds);
            }
            else
            {
                logger.LogWarning("[NW.ConnectionGuard] DDoS-detected ip={Address}", address);
            }
        }
    }

    /// <summary>
    /// Logs a connection ban rejection warning with throttled behavior.
    /// </summary>
    public static void LogBanned(ILogger? logger, ConnectionGuard.ConnectionLimitEntry entry, string address, long nowTicks, long windowTicks, DateTime bannedUntil)
    {
        if (TryAcquire(
                ref entry.LastRejectLogTicks,
                ref entry.SuppressedRejectCount,
                nowTicks, windowTicks,
                out long suppressed))
        {
            string suffix = suppressed > 0 ? $" (+{suppressed} suppressed)" : string.Empty;

            if (logger != null && logger.IsEnabled(LogLevel.Trace))
            {
                string bannedTime = bannedUntil.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                logger.LogTrace("[NW.ConnectionGuard] banned-reject ip={Address} until={BannedUntil}{Suffix}", address, bannedTime, suffix);
            }
        }
    }
}
