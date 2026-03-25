// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Framework.Time;

public static partial class Clock
{
    /// <summary>
    /// Custom epoch (Unix ms) used for ID generation.
    /// Default: 2025-01-01 UTC.
    /// </summary>
    public static readonly long EpochMilliseconds = new System.DateTimeOffset(2025, 1, 1, 0, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// Returns milliseconds since custom epoch.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static long EpochMillisecondsNow() => UnixMillisecondsNow() - EpochMilliseconds;

    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.DateTime NowUtc()
    {
        long swTicks = UtcStopwatch.ElapsedTicks;

        if (!IsSynchronized)
        {
            long ticks = UtcBaseTicks + (long)(swTicks * _swToDateTimeTicks);
            return new System.DateTime(ticks, System.DateTimeKind.Utc);
        }

        // Use Volatile.Read to ensure thread-safe reads of synchronized values
        double dc = System.Threading.Volatile.Read(ref _driftCorrection);
        long offset = System.Threading.Volatile.Read(ref _timeOffset);

        // Apply drift correction to the entire elapsed time, then add offset
        long corrected = (long)(swTicks * _swToDateTimeTicks * dc) + offset;
        return new System.DateTime(UtcBaseTicks + corrected, System.DateTimeKind.Utc);
    }

    /// <summary>
    /// Current Unix timestamp (seconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static long UnixSecondsNow() => (long)(NowUtc() - System.DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Current Unix timestamp (seconds) as uint32.
    /// Note: uint32 max is ~4.2 billion, Unix seconds is currently ~1.7 billion (since 1970),
    /// so it's OK for now but will overflow in ~50 years (around year 2106).
    /// </summary>
    /// <returns>The current Unix timestamp in seconds as uint32.</returns>
    /// <exception cref="System.OverflowException">Thrown when the Unix timestamp exceeds UInt32.MaxValue.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static uint UnixSecondsNowUInt32()
    {
        long seconds = (long)(NowUtc() - System.DateTime.UnixEpoch).TotalSeconds;

        // Check for overflow before casting
        return seconds > uint.MaxValue
            ? throw new System.OverflowException(
                "Unix timestamp exceeds UInt32.MaxValue. This typically occurs after year 2106.")
            : seconds < 0
            ? throw new System.OverflowException(
                "Unix timestamp is negative, indicating time before Unix epoch.")
            : (uint)seconds;
    }

    /// <summary>
    /// Current Unix timestamp (milliseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static long UnixMillisecondsNow() => (long)(NowUtc() - System.DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (microseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static long UnixMicrosecondsNow() => (NowUtc() - System.DateTime.UnixEpoch).Ticks / 10;

    /// <summary>
    /// Current Unix timestamp (ticks) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static long UnixTicksNow() => (NowUtc() - System.DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Returns the current Unix time as TimeSpan.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.TimeSpan UnixTime() => NowUtc() - System.DateTime.UnixEpoch;

    /// <summary>
    /// Returns the current monotonic tick count using <see cref="System.Diagnostics.Stopwatch"/>.
    /// These ticks are monotonic (not affected by system clock changes),
    /// suitable for latency/RTT measurement.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static long MonoTicksNow() => System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts a monotonic tick delta into milliseconds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static double MonoTicksToMilliseconds(
        [System.Diagnostics.CodeAnalysis.NotNull] long tickDelta) => tickDelta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
}
