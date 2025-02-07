// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Time;

public static partial class Clock
{
    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.DateTime NowUtc()
    {
        System.Int64 swTicks = UtcStopwatch.ElapsedTicks;

        if (!IsSynchronized)
        {
            System.Int64 ticks = UtcBaseTicks + (System.Int64)(swTicks * _swToDateTimeTicks);
            return new System.DateTime(ticks, System.DateTimeKind.Utc);
        }

        // Use Volatile.Read to ensure thread-safe reads of synchronized values
        System.Double dc = System.Threading.Volatile.Read(ref _driftCorrection);
        System.Int64 offset = System.Threading.Volatile.Read(ref _timeOffset);

        // Apply drift correction to the entire elapsed time, then add offset
        System.Int64 corrected = (System.Int64)(swTicks * _swToDateTimeTicks * dc) + offset;
        return new System.DateTime(UtcBaseTicks + corrected, System.DateTimeKind.Utc);
    }

    /// <summary>
    /// Current Unix timestamp (seconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int64 UnixSecondsNow() => (System.Int64)(NowUtc() - System.DateTime.UnixEpoch).TotalSeconds;

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
    public static System.UInt32 UnixSecondsNowUInt32()
    {
        System.Int64 seconds = (System.Int64)(NowUtc() - System.DateTime.UnixEpoch).TotalSeconds;
        
        // Check for overflow before casting
        if (seconds > System.UInt32.MaxValue)
        {
            throw new System.OverflowException(
                "Unix timestamp exceeds UInt32.MaxValue. This typically occurs after year 2106.");
        }
        
        if (seconds < 0)
        {
            throw new System.OverflowException(
                "Unix timestamp is negative, indicating time before Unix epoch.");
        }

        return (System.UInt32)seconds;
    }

    /// <summary>
    /// Current Unix timestamp (milliseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int64 UnixMillisecondsNow() => (System.Int64)(NowUtc() - System.DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (microseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int64 UnixMicrosecondsNow() => (NowUtc() - System.DateTime.UnixEpoch).Ticks / 10;

    /// <summary>
    /// Current Unix timestamp (ticks) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int64 UnixTicksNow() => (NowUtc() - System.DateTime.UnixEpoch).Ticks;

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
    public static System.Int64 MonoTicksNow() => System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts a monotonic tick delta into milliseconds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Double MonoTicksToMilliseconds(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int64 tickDelta) => tickDelta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
}
