
namespace Nalix.Framework.Time;

public static partial class Clock
{
    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime NowUtc()
    {
        System.Int64 swTicks = UtcStopwatch.ElapsedTicks;

        if (!IsSynchronized)
        {
            System.Int64 ticks = UtcBaseTicks + (System.Int64)(swTicks * _swToDateTimeTicks);
            return new System.DateTime(ticks, System.DateTimeKind.Utc);
        }

        System.Double dc = _driftCorrection;
        System.Int64 offset = _timeOffset;

        System.Int64 corrected = (System.Int64)(swTicks * _swToDateTimeTicks * dc) + offset;
        return new System.DateTime(UtcBaseTicks + corrected, System.DateTimeKind.Utc);
    }

    /// <summary>
    /// Current Unix timestamp (seconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int64 UnixSecondsNow() => (System.Int64)(NowUtc() - System.DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Current Unix timestamp (milliseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int64 UnixMillisecondsNow() => (System.Int64)(NowUtc() - System.DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (microseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int64 UnixMicrosecondsNow() => (NowUtc() - System.DateTime.UnixEpoch).Ticks / 10;

    /// <summary>
    /// Current Unix timestamp (ticks) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int64 UnixTicksNow() => (NowUtc() - System.DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Returns the current Unix time as TimeSpan.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.TimeSpan UnixTime() => NowUtc() - System.DateTime.UnixEpoch;

    /// <summary>
    /// Returns the current monotonic tick count using <see cref="System.Diagnostics.Stopwatch"/>.
    /// These ticks are monotonic (not affected by system clock changes),
    /// suitable for latency/RTT measurement.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int64 MonoTicksNow() => System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts a monotonic tick delta into milliseconds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Double MonoTicksToMilliseconds(System.Int64 tickDelta) => tickDelta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
}
