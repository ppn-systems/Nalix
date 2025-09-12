namespace Nalix.Framework.Time;

public static partial class Clock
{
    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime GetUtcNowPrecise()
    {
        System.TimeSpan elapsed = UtcStopwatch.Elapsed;
        if (IsSynchronized)
        {
            // Apply drift correction and offset
            System.Int64 correctedTicks = (System.Int64)(elapsed.Ticks * _driftCorrection) + _timeOffset;
            return UtcBase.AddTicks(correctedTicks);
        }
        return UtcBase.Add(elapsed);
    }

    /// <summary>
    /// Returns the current UTC time with high accuracy, formatted as a string.
    /// </summary>
    /// <param name="format">The format string to use for formatting the date and time.</param>
    /// <returns>A string representation of the current UTC time.</returns>
    public static System.String GetUtcNowString(System.String format = "yyyy-MM-dd HH:mm:ss.fff")
        => GetUtcNowPrecise().ToString(format);

    /// <summary>
    /// Current Unix timestamp (seconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int64 UnixSecondsNow()
        => (System.Int64)(GetUtcNowPrecise() - System.DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Current Unix timestamp (milliseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int64 UnixMillisecondsNow()
        => (System.Int64)(GetUtcNowPrecise() - System.DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (microseconds) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int64 UnixMicrosecondsNow()
        => (GetUtcNowPrecise() - System.DateTime.UnixEpoch).Ticks / 10;

    /// <summary>
    /// Current Unix timestamp (ticks) as long.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int64 UnixTicksNow()
        => (GetUtcNowPrecise() - System.DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Returns the current Unix time as TimeSpan.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan UnixTime() => GetUtcNowPrecise() - System.DateTime.UnixEpoch;

    /// <summary>
    /// Returns the current application-specific time as TimeSpan (relative to TimeEpoch).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan ApplicationTime() => GetUtcNowPrecise() - TimeEpoch;

    /// <summary>
    /// Returns the raw, unadjusted system time without synchronization or drift correction.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime GetRawUtcNow() => System.DateTime.UtcNow;

    /// <summary>
    /// Returns the current monotonic tick count using <see cref="System.Diagnostics.Stopwatch"/>.
    /// These ticks are monotonic (not affected by system clock changes),
    /// suitable for latency/RTT measurement.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int64 MonoTicksNow()
        => System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts a monotonic tick delta into milliseconds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Double MonoTicksToMilliseconds(System.Int64 tickDelta)
        => tickDelta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
}
