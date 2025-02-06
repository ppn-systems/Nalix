using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Shared.Time;

/// <summary>
/// Handles precise time for the system with high accuracy.
/// </summary>
public static class Clock
{
    public const ulong TimeEpochTimestamp = 1577836800UL; // (Wed Jan 01 2020 00:00:00)
    public static readonly DateTime TimeEpochDatetime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TimeEpoch = DateTime.UnixEpoch.AddSeconds(TimeEpochTimestamp);

    private static readonly DateTime _utcBase = DateTime.UtcNow;
    private static readonly Stopwatch _utcStopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime GetUtcNowPrecise() => _utcBase.Add(_utcStopwatch.Elapsed);

    /// <summary>
    /// Current Unix timestamp (seconds) as ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong UnixSecondsNow() =>
        (ulong)(GetUtcNowPrecise() - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Current Unix timestamp (milliseconds) as ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong UnixMillisecondsNow() =>
        (ulong)(GetUtcNowPrecise() - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (ticks) as ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong UnixTicksNow() =>
        (ulong)(GetUtcNowPrecise() - DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Returns the current Unix time as TimeSpan.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan UnixTime() =>
        TimeSpan.FromMilliseconds(UnixMillisecondsNow());

    /// <summary>
    /// Converts Unix timestamp (milliseconds) to DateTime with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixTimeMillisecondsToDateTime(ulong timestamp)
    {
        if (timestamp > (ulong)DateTime.MaxValue.Subtract(DateTime.UnixEpoch).TotalMilliseconds)
            throw new OverflowException("Timestamp exceeds DateTime limits");
        return DateTime.UnixEpoch.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// Converts timestamp (milliseconds) to DateTime with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime TimeMillisecondsToDateTime(ulong timestamp)
    {
        if (timestamp > (ulong)DateTime.MaxValue.Subtract(TimeEpoch).TotalMilliseconds)
            throw new OverflowException("Timestamp exceeds DateTime limits");
        return TimeEpoch.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// Converts TimeSpan to DateTime with validation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixTimeToDateTime(TimeSpan timeSpan)
    {
        if (timeSpan.Ticks < 0)
            throw new ArgumentException("TimeSpan cannot be negative", nameof(timeSpan));
        return DateTime.UnixEpoch.Add(timeSpan);
    }

    /// <summary>
    /// Converts DateTime to Unix TimeSpan with validation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan DateTimeToUnixTime(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime must be UTC", nameof(dateTime));
        return dateTime - DateTime.UnixEpoch;
    }

    /// <summary>
    /// Converts DateTime to game TimeSpan with validation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan DateTimeToTime(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime must be UTC", nameof(dateTime));
        return dateTime - TimeEpoch;
    }

    /// <summary>
    /// Compares two TimeSpan values and returns the greater one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Max(TimeSpan time1, TimeSpan time2) =>
        time1 > time2 ? time1 : time2;

    /// <summary>
    /// Compares two TimeSpan values and returns the lesser one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Min(TimeSpan time1, TimeSpan time2) =>
        time1 < time2 ? time1 : time2;

    /// <summary>
    /// Checks if a DateTime is within the allowed range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(DateTime dateTime, TimeSpan range)
    {
        var diff = GetUtcNowPrecise() - dateTime;
        return diff.Ticks >= -range.Ticks && diff.Ticks <= range.Ticks;
    }
}