// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Framework.Time;

public static partial class Clock
{
    /// <summary>
    /// Custom epoch (Unix ms) used for ID generation.
    /// Default: 2025-01-01 UTC.
    /// </summary>
    public static readonly long EpochMilliseconds = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// Returns milliseconds since custom epoch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long EpochMillisecondsNow() => UnixMillisecondsNow() - EpochMilliseconds;

    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static DateTime NowUtc()
    {
        long swTicks = UtcStopwatch.ElapsedTicks;

        if (!IsSynchronized)
        {
            long ticks = UtcBaseTicks + (long)(swTicks * _swToDateTimeTicks);
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        // Use Volatile.Read to ensure thread-safe reads of synchronized values
        double dc = Volatile.Read(ref _driftCorrection);
        long offset = Volatile.Read(ref _timeOffset);

        // Apply drift correction to the entire elapsed time, then add offset
        long corrected = (long)(swTicks * _swToDateTimeTicks * dc) + offset;
        return new DateTime(UtcBaseTicks + corrected, DateTimeKind.Utc);
    }

    /// <summary>
    /// Current Unix timestamp (seconds) as long.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static long UnixSecondsNow() => (long)(NowUtc() - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Current Unix timestamp (seconds) as uint32.
    /// Note: uint32 max is ~4.2 billion, Unix seconds is currently ~1.7 billion (since 1970),
    /// so it's OK for now but will overflow in ~50 years (around year 2106).
    /// </summary>
    /// <returns>The current Unix timestamp in seconds as uint32.</returns>
    /// <exception cref="OverflowException">Thrown when the Unix timestamp exceeds UInt32.MaxValue.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static uint UnixSecondsNowUInt32()
    {
        long seconds = (long)(NowUtc() - DateTime.UnixEpoch).TotalSeconds;

        // Check for overflow before casting
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static long UnixMillisecondsNow() => (long)(NowUtc() - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (microseconds) as long.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static long UnixMicrosecondsNow() => (NowUtc() - DateTime.UnixEpoch).Ticks / 10;

    /// <summary>
    /// Current Unix timestamp (ticks) as long.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static long UnixTicksNow() => (NowUtc() - DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Returns the current Unix time as TimeSpan.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static TimeSpan UnixTime() => NowUtc() - DateTime.UnixEpoch;

    /// <summary>
    /// Returns the current monotonic tick count using <see cref="Stopwatch"/>.
    /// These ticks are monotonic (not affected by system clock changes),
    /// suitable for latency/RTT measurement.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static long MonoTicksNow() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts a monotonic tick delta into milliseconds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static double MonoTicksToMilliseconds(
        long tickDelta) => tickDelta * 1000.0 / Stopwatch.Frequency;
}
