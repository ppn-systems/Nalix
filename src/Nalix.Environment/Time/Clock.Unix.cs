// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Environment.Time;

public static partial class Clock
{
    /// <summary>
    /// Custom epoch (Unix ms) used for ID generation.
    /// Default: 2025-01-01 UTC.
    /// This keeps project-local IDs compact and makes timestamp math independent
    /// from the raw 1970 Unix epoch.
    /// </summary>
    public static readonly long EpochMilliseconds = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// Returns milliseconds since the custom epoch.
    /// This is useful for compact IDs and monotonic timestamp deltas where the
    /// absolute Unix time is not needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long EpochMillisecondsNow() => UnixMillisecondsNow() - EpochMilliseconds;

    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// The value is reconstructed from the monotonic stopwatch plus the last
    /// synchronization state so it does not jump around with small clock changes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed synchronized time falls outside the valid <see cref="DateTime"/> range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime NowUtc()
    {
        long swTicks = s_utcStopwatch.ElapsedTicks;

        if (!IsSynchronized)
        {
            long ticks = s_utcBaseTicks + (long)(swTicks * s_swToDateTimeTicks);
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        // Use volatile reads so synchronization updates become visible without
        // locking every time query.
        double dc = Volatile.Read(ref s_driftCorrection);
        long offset = Volatile.Read(ref s_timeOffset);

        // Apply drift correction to the elapsed stopwatch span, then add the
        // stored offset to recover the current UTC estimate.
        long corrected = (long)(swTicks * s_swToDateTimeTicks * dc) + offset;
        return new DateTime(s_utcBaseTicks + corrected, DateTimeKind.Utc);
    }

    /// <summary>
    /// Current Unix timestamp (seconds) as long.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed synchronized time falls outside the valid <see cref="DateTime"/> range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static long UnixSecondsNow() => (long)(NowUtc() - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Current Unix timestamp (seconds) as uint32.
    /// Note: uint32 max is ~4.2 billion, Unix seconds is currently ~1.7 billion
    /// (since 1970), so it is still safe for now but will overflow around 2106.
    /// </summary>
    /// <returns>The current Unix timestamp in seconds as uint32.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed synchronized time falls outside the valid <see cref="DateTime"/> range.</exception>
    /// <exception cref="OverflowException">Thrown when the Unix timestamp exceeds UInt32.MaxValue.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint UnixSecondsNowUInt32()
    {
        long seconds = (long)(NowUtc() - DateTime.UnixEpoch).TotalSeconds;

        // Check for overflow before casting so the caller sees a clear exception
        // instead of a silent wraparound.
        return seconds > uint.MaxValue
            ? throw new OverflowException(
                "Unix timestamp exceeds UInt32.MaxValue. This typically occurs after year 2106.")
            : seconds < 0
            ? throw new OverflowException(
                "Unix timestamp is negative, indicating time before Unix epoch.")
            : (uint)seconds;
    }

    /// <summary>
    /// Current Unix timestamp (milliseconds) as long.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed synchronized time falls outside the valid <see cref="DateTime"/> range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UnixMillisecondsNow() => (long)(NowUtc() - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (microseconds) as long.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed synchronized time falls outside the valid <see cref="DateTime"/> range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static long UnixMicrosecondsNow() => (NowUtc() - DateTime.UnixEpoch).Ticks / 10;

    /// <summary>
    /// Current Unix timestamp (ticks) as long.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed synchronized time falls outside the valid <see cref="DateTime"/> range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static long UnixTicksNow() => (NowUtc() - DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Returns the current Unix time as TimeSpan.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the computed synchronized time falls outside the valid <see cref="DateTime"/> range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan UnixTime() => NowUtc() - DateTime.UnixEpoch;

    /// <summary>
    /// Returns the current monotonic tick count using <see cref="Stopwatch"/>.
    /// These ticks are monotonic (not affected by system clock changes),
    /// suitable for latency/RTT measurement.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long MonoTicksNow() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts a monotonic tick delta into milliseconds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static double MonoTicksToMilliseconds(
        long tickDelta) => tickDelta * 1000.0 / Stopwatch.Frequency;
}
