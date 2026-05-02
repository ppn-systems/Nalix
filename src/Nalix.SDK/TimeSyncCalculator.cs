// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Environment.Time;

namespace Nalix.SDK;

/// <summary>
/// Provides client-side calculations for synchronizing time with a remote server.
/// </summary>
public static class TimeSyncCalculator
{
    /// <summary>
    /// Synchronizes the local client clock with a remote server timestamp (t3),
    /// compensating for half the round-trip latency (RTT/2).
    /// 
    /// This method assumes:
    /// - serverUnixMs = server send time (t3)
    /// - rttMs = measured round-trip time
    /// 
    /// Client-only. Do NOT use on server.
    /// </summary>
    /// <param name="serverUnixMs">The server Unix timestamp, in milliseconds.</param>
    /// <param name="rttMs">The measured round-trip time, in milliseconds.</param>
    /// <param name="maxAllowedDriftMs">The maximum drift, in milliseconds, allowed before the correction is skipped.</param>
    /// <param name="maxHardAdjustMs">The maximum absolute correction, in milliseconds, allowed for a single adjustment.</param>
    /// <returns>The computed adjustment offset, in milliseconds, or 0 when the correction is rejected.</returns>
    /// <exception cref="ArgumentException">Thrown when an input value is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateOffsetMs(
        long serverUnixMs,
        double rttMs = 0,
        double maxAllowedDriftMs = 1_000.0,
        double maxHardAdjustMs = 10_000.0)
    {
        // -------- VALIDATION --------
        if (serverUnixMs < 0)
        {
            throw new ArgumentException("Server Unix timestamp cannot be negative", nameof(serverUnixMs));
        }

        if (rttMs < 0)
        {
            throw new ArgumentException("RTT cannot be negative", nameof(rttMs));
        }

        if (maxAllowedDriftMs <= 0)
        {
            throw new ArgumentException("Max allowed drift must be positive", nameof(maxAllowedDriftMs));
        }

        if (maxHardAdjustMs <= 0)
        {
            throw new ArgumentException("Max hard adjust must be positive", nameof(maxHardAdjustMs));
        }

        // sanity check timestamp
        const long MinUnix = 946684800000L;
        const long MaxUnix = 4102444800000L;
        if (serverUnixMs is < MinUnix or > MaxUnix)
        {
            throw new ArgumentException("Server Unix timestamp is outside reasonable range", nameof(serverUnixMs));
        }

        // -------- CORE --------
        long correctedUnixMs = serverUnixMs - (long)(rttMs * 0.5);
        long localNow = Clock.UnixMillisecondsNow();

        double offsetMs = correctedUnixMs - localNow;

        // -------- HARD REJECT --------
        if (Math.Abs(offsetMs) > maxHardAdjustMs)
        {
            return 0;
        }

        // -------- SOFT CLAMP --------
        if (Math.Abs(offsetMs) > maxAllowedDriftMs)
        {
            offsetMs = Math.Sign(offsetMs) * maxAllowedDriftMs;
        }

        return offsetMs;
    }
}
