using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Shared.Time;

/// <summary>
/// Handles precise time for the system with high accuracy, supporting various time-related operations
/// required for real-time communication and distributed systems.
/// </summary>
public static class Clock
{
    #region Constants and Fields

    /// <summary>
    /// The Unix timestamp representing the start of 2020 (Wed Jan 01 2020 00:00:00 UTC).
    /// </summary>
    public const long TimeEpochTimestamp = 1577836800L; // (Wed Jan 01 2020 00:00:00)

    /// <summary>
    /// The <see cref="DateTime"/> representation of the start of 2020 (Wed Jan 01 2020 00:00:00 UTC).
    /// </summary>
    public static readonly DateTime TimeEpochDatetime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // BaseValue36 values for high-precision time calculation
    private static readonly DateTime UtcBase = DateTime.UtcNow;

    private static readonly Stopwatch UtcStopwatch = Stopwatch.StartNew();
    private static readonly DateTime TimeEpoch = DateTime.UnixEpoch.AddSeconds(TimeEpochTimestamp);

    // Time synchronization variables
    private static long _timeOffset = 0; // In ticks, adjusted from external time sources

    private static double _driftCorrection = 1.0; // Multiplier to correct for system clock drift
    private static bool _isSynchronized = false;
    private static DateTime _lastSyncTime = DateTime.MinValue;

    // Performance measurement fields
    private static readonly ThreadLocal<Stopwatch> _threadStopwatch = new(() => Stopwatch.StartNew());

    // Frequency information for high-resolution timing
    private static readonly double _tickFrequency = 1.0 / Stopwatch.Frequency;

    private static readonly bool _isHighResolution = Stopwatch.IsHighResolution;

    #endregion Constants and Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the system clock is using high-resolution timing.
    /// </summary>
    public static bool IsHighResolution => _isHighResolution;

    /// <summary>
    /// Gets the tick frequency in seconds (the duration of a single tick).
    /// </summary>
    public static double TickFrequency => _tickFrequency;

    /// <summary>
    /// Gets the frequency of the high-resolution timer in ticks per second.
    /// </summary>
    public static long TicksPerSecond => Stopwatch.Frequency;

    /// <summary>
    /// Gets a value indicating whether the clock has been synchronized with an external time source.
    /// </summary>
    public static bool IsSynchronized => _isSynchronized;

    /// <summary>
    /// Gets the time when the last synchronization occurred.
    /// </summary>
    public static DateTime LastSyncTime => _lastSyncTime;

    /// <summary>
    /// Gets the current offset in milliseconds between the system time and the synchronized time.
    /// </summary>
    public static double CurrentOffsetMs => TimeSpan.FromTicks(_timeOffset).TotalMilliseconds;

    #endregion Properties

    #region Basic Time Functions

    /// <summary>
    /// Returns the current UTC time with high accuracy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime GetUtcNowPrecise()
    {
        TimeSpan elapsed = UtcStopwatch.Elapsed;
        if (_isSynchronized)
        {
            // Apply drift correction and offset
            long correctedTicks = (long)(elapsed.Ticks * _driftCorrection) + _timeOffset;
            return UtcBase.AddTicks(correctedTicks);
        }
        return UtcBase.Add(elapsed);
    }

    /// <summary>
    /// Returns the current UTC time with high accuracy, formatted as a string.
    /// </summary>
    /// <param name="format">The format string to use for formatting the date and time.</param>
    /// <returns>A string representation of the current UTC time.</returns>
    public static string GetUtcNowString(string format = "yyyy-MM-dd HH:mm:ss.fff")
        => GetUtcNowPrecise().ToString(format);

    /// <summary>
    /// Current Unix timestamp (seconds) as ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UnixSecondsNow()
        => (long)(GetUtcNowPrecise() - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>
    /// Current Unix timestamp (milliseconds) as ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UnixMillisecondsNow()
        => (long)(GetUtcNowPrecise() - DateTime.UnixEpoch).TotalMilliseconds;

    /// <summary>
    /// Current Unix timestamp (microseconds) as ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UnixMicrosecondsNow()
        => (long)(GetUtcNowPrecise() - DateTime.UnixEpoch).Ticks / 10;

    /// <summary>
    /// Current Unix timestamp (ticks) as ulong.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UnixTicksNow()
        => (GetUtcNowPrecise() - DateTime.UnixEpoch).Ticks;

    /// <summary>
    /// Returns the current Unix time as TimeSpan.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan UnixTime() => GetUtcNowPrecise() - DateTime.UnixEpoch;

    /// <summary>
    /// Returns the current application-specific time as TimeSpan (relative to TimeEpoch).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan ApplicationTime() => GetUtcNowPrecise() - TimeEpoch;

    /// <summary>
    /// Returns the raw, unadjusted system time without synchronization or drift correction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime GetRawUtcNow() => DateTime.UtcNow;

    #endregion Basic Time Functions

    #region Conversion Methods

    /// <summary>
    /// Converts Unix timestamp (seconds) to DateTime with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixTimeSecondsToDateTime(long timestamp)
    {
        if (timestamp > (long)DateTime.MaxValue.Subtract(DateTime.UnixEpoch).TotalSeconds)
            throw new OverflowException("Timestamp exceeds DateTime limits");
        return DateTime.UnixEpoch.AddSeconds(timestamp);
    }

    /// <summary>
    /// Converts Unix timestamp (milliseconds) to DateTime with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixTimeMillisecondsToDateTime(long timestamp)
    {
        if (timestamp > (long)DateTime.MaxValue.Subtract(DateTime.UnixEpoch).TotalMilliseconds)
            throw new OverflowException("Timestamp exceeds DateTime limits");

        return DateTime.UnixEpoch.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// Converts Unix timestamp (microseconds) to DateTime with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixTimeMicrosecondsToDateTime(long timestamp)
    {
        if (timestamp > (DateTime.MaxValue.Subtract(DateTime.UnixEpoch).Ticks / 10))
            throw new OverflowException("Timestamp exceeds DateTime limits");

        return DateTime.UnixEpoch.AddTicks(timestamp * 10);
    }

    /// <summary>
    /// Converts timestamp (milliseconds) to DateTime with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime TimeMillisecondsToDateTime(long timestamp)
    {
        if (timestamp > (long)DateTime.MaxValue.Subtract(TimeEpoch).TotalMilliseconds)
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
    /// Converts DateTime to application-specific TimeSpan with validation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan DateTimeToTime(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime must be UTC", nameof(dateTime));

        return dateTime - TimeEpoch;
    }

    /// <summary>
    /// Converts UTC DateTime to Unix timestamp in seconds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong DateTimeToUnixTimeSeconds(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime must be UTC", nameof(utcDateTime));

        return (ulong)(utcDateTime - DateTime.UnixEpoch).TotalSeconds;
    }

    /// <summary>
    /// Converts UTC DateTime to Unix timestamp in milliseconds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong DateTimeToUnixTimeMilliseconds(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime must be UTC", nameof(utcDateTime));

        return (ulong)(utcDateTime - DateTime.UnixEpoch).TotalMilliseconds;
    }

    #endregion Conversion Methods

    #region Comparison Methods

    /// <summary>
    /// Compares two TimeSpan values and returns the greater one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Max(TimeSpan time1, TimeSpan time2)
        => time1 > time2 ? time1 : time2;

    /// <summary>
    /// Compares two TimeSpan values and returns the lesser one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Min(TimeSpan time1, TimeSpan time2)
        => time1 < time2 ? time1 : time2;

    /// <summary>
    /// Checks if a DateTime is within the allowed range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(DateTime dateTime, TimeSpan range)
    {
        TimeSpan diff = GetUtcNowPrecise() - dateTime;
        return diff.Ticks >= -range.Ticks && diff.Ticks <= range.Ticks;
    }

    /// <summary>
    /// Clamps a DateTime between a minimum and maximum value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime Clamp(DateTime value, DateTime min, DateTime max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Clamps a TimeSpan between a minimum and maximum value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    #endregion Comparison Methods

    #region Time Synchronization Methods

    /// <summary>
    /// Synchronizes the clock with an external time source.
    /// </summary>
    /// <param name="externalTime">The accurate external UTC time.</param>
    /// <param name="maxAllowedDriftMs">Maximum allowed drift in milliseconds before adjustment is applied.</param>
    /// <returns>The adjustment made in milliseconds.</returns>
    public static double SynchronizeTime(DateTime externalTime, double maxAllowedDriftMs = 1000.0)
    {
        if (externalTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("External time must be UTC", nameof(externalTime));

        // Calculate current time according to our clock
        DateTime currentTime = GetUtcNowPrecise();

        // Calculate time difference
        var timeDifference = externalTime - currentTime;

        // Only adjust if drift is above threshold
        if (Math.Abs(timeDifference.TotalMilliseconds) <= maxAllowedDriftMs)
            return 0;

        // Calculate and apply offset
        _timeOffset = timeDifference.Ticks;
        _isSynchronized = true;
        _lastSyncTime = externalTime;

        // Calculate drift rate if we have previous sync data
        if (_lastSyncTime != DateTime.MinValue)
        {
            var elapsedSinceLastSync = (externalTime - _lastSyncTime).TotalSeconds;
            if (elapsedSinceLastSync > 60) // Only calculate drift after at least 1 minute
            {
                var expectedElapsed = (currentTime - UtcBase).TotalSeconds;
                var actualElapsed = (externalTime - UtcBase).TotalSeconds;
                _driftCorrection = actualElapsed / expectedElapsed;
            }
        }

        return timeDifference.TotalMilliseconds;
    }

    /// <summary>
    /// Resets time synchronization to use the local system time.
    /// </summary>
    public static void ResetSynchronization()
    {
        _timeOffset = 0;
        _driftCorrection = 1.0;
        _isSynchronized = false;
        _lastSyncTime = DateTime.MinValue;
    }

    /// <summary>
    /// Gets the estimated clock drift rate.
    /// A value greater than 1.0 means the local clock is running slower than the reference clock.
    /// A value less than 1.0 means the local clock is running faster than the reference clock.
    /// </summary>
    public static double GetDriftRate() => _driftCorrection;

    /// <summary>
    /// Gets the current error estimate between the synchronized time and system time in milliseconds.
    /// </summary>
    public static double GetCurrentErrorEstimateMs()
    {
        if (!_isSynchronized)
            return 0;

        var driftedTime = GetUtcNowPrecise();
        var systemTime = DateTime.UtcNow;
        return (driftedTime - systemTime).TotalMilliseconds;
    }

    #endregion Time Synchronization Methods

    #region Performance Measurement

    /// <summary>
    /// Starts a new performance measurement using the current thread's stopwatch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StartMeasurement() => _threadStopwatch.Value!.Restart();

    /// <summary>
    /// Gets the elapsed time since the last call to StartMeasurement() for the current thread.
    /// </summary>
    /// <returns>The elapsed time as a TimeSpan.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan GetElapsed() => _threadStopwatch.Value!.Elapsed;

    /// <summary>
    /// Gets the elapsed time in milliseconds since the last call to StartMeasurement() for the current thread.
    /// </summary>
    /// <returns>The elapsed time in milliseconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetElapsedMilliseconds()
        => _threadStopwatch.Value!.Elapsed.TotalMilliseconds;

    /// <summary>
    /// Gets the elapsed time in microseconds since the last call to StartMeasurement() for the current thread.
    /// </summary>
    /// <returns>The elapsed time in microseconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetElapsedMicroseconds()
        => _threadStopwatch.Value!.Elapsed.Ticks / 10.0;

    /// <summary>
    /// Creates a new TimeStamp for precise interval measurement.
    /// </summary>
    /// <returns>A TimeStamp object representing the current time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeStamp CreateTimeStamp() => new(Stopwatch.GetTimestamp());

    /// <summary>
    /// Measures the execution time of an action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    public static double MeasureExecutionTime(Action action)
    {
        StartMeasurement();
        action();
        return GetElapsedMilliseconds();
    }

    /// <summary>
    /// Executes an action and returns both the result and the execution time.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="func">The function to execute and measure.</param>
    /// <returns>A tuple containing the result and the elapsed time in milliseconds.</returns>
    public static (T Result, double ElapsedMs) MeasureFunction<T>(Func<T> func)
    {
        StartMeasurement();
        T result = func();
        return (result, GetElapsedMilliseconds());
    }

    #endregion Performance Measurement

    #region Time-based Operations

    /// <summary>
    /// Waits until a specific UTC time has been reached.
    /// </summary>
    /// <param name="targetTime">The UTC time to wait for.</param>
    /// <param name="cancellationToken">Optional cancellation token to abort the wait.</param>
    /// <returns>True if the target time was reached; false if the wait was cancelled.</returns>
    public static bool WaitUntil(DateTime targetTime, CancellationToken cancellationToken = default)
    {
        if (targetTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Target time must be UTC", nameof(targetTime));

        while (GetUtcNowPrecise() < targetTime)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            // Calculate how long to sleep
            var remainingTime = targetTime - GetUtcNowPrecise();
            if (remainingTime <= TimeSpan.Zero)
                break;

            // For longer waits, sleep in chunks to allow cancellation checks
            if (remainingTime.TotalMilliseconds > 50)
                Thread.Sleep(Math.Min((int)remainingTime.TotalMilliseconds / 2, 50));
            else if (remainingTime.TotalMilliseconds > 1)
                Thread.Sleep(1);
            else
                Thread.SpinWait(100); // For sub-millisecond precision
        }

        return !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Returns true if the current time is between the start and end times.
    /// </summary>
    /// <param name="startTime">The start time (inclusive).</param>
    /// <param name="endTime">The end time (exclusive).</param>
    /// <returns>True if the current time is within the specified range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTimeBetween(DateTime startTime, DateTime endTime)
    {
        var now = GetUtcNowPrecise();
        return now >= startTime && now < endTime;
    }

    /// <summary>
    /// Calculates the time remaining until a specified time.
    /// </summary>
    /// <param name="targetTime">The target UTC time.</param>
    /// <returns>The TimeSpan until the target time, or TimeSpan.Zero if the target time has passed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan GetTimeRemaining(DateTime targetTime)
    {
        var timeRemaining = targetTime - GetUtcNowPrecise();
        return timeRemaining > TimeSpan.Zero ? timeRemaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Determines if a given time has elapsed since a start time.
    /// </summary>
    /// <param name="startTime">The start time (UTC).</param>
    /// <param name="duration">The duration to check.</param>
    /// <returns>True if the duration has elapsed since the start time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasElapsed(DateTime startTime, TimeSpan duration)
        => GetUtcNowPrecise() - startTime >= duration;

    /// <summary>
    /// Gets the percentage of completion between a start time and an end time.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <returns>A value between 0.0 and 1.0 representing the percentage completion.</returns>
    public static double GetPercentageComplete(DateTime startTime, DateTime endTime)
    {
        if (startTime >= endTime)
            return 1.0;

        var totalDuration = endTime - startTime;
        var elapsed = GetUtcNowPrecise() - startTime;

        if (elapsed <= TimeSpan.Zero)
            return 0.0;

        if (elapsed >= totalDuration)
            return 1.0;

        return elapsed.TotalMilliseconds / totalDuration.TotalMilliseconds;
    }

    #endregion Time-based Operations

    #region Time Formatting

    /// <summary>
    /// Formats a TimeSpan as a human-readable string (e.g., "2d 5h 30m 15s").
    /// </summary>
    /// <param name="timeSpan">The TimeSpan to format.</param>
    /// <param name="includeMilliseconds">Whether to include milliseconds in the output.</param>
    /// <returns>A formatted string representation of the TimeSpan.</returns>
    public static string FormatTimeSpan(TimeSpan timeSpan, bool includeMilliseconds = false)
    {
        if (timeSpan == TimeSpan.Zero)
            return "0s";

        var parts = new List<string>();

        if (timeSpan.Days > 0)
            parts.Add($"{timeSpan.Days}d");

        if (timeSpan.Hours > 0)
            parts.Add($"{timeSpan.Hours}h");

        if (timeSpan.Minutes > 0)
            parts.Add($"{timeSpan.Minutes}m");

        if (timeSpan.Seconds > 0 || (parts.Count == 0 && !includeMilliseconds))
            parts.Add($"{timeSpan.Seconds}s");

        if (includeMilliseconds && (timeSpan.Milliseconds > 0 || parts.Count == 0))
            parts.Add($"{timeSpan.Milliseconds}ms");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Formats the time elapsed since a specified UTC DateTime as a human-readable string.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="includeMilliseconds">Whether to include milliseconds in the output.</param>
    /// <returns>A formatted string representing the elapsed time.</returns>
    public static string FormatElapsedTime(DateTime startTime, bool includeMilliseconds = false)
    {
        if (startTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Start time must be UTC", nameof(startTime));

        return FormatTimeSpan(GetUtcNowPrecise() - startTime, includeMilliseconds);
    }

    /// <summary>
    /// Returns a human-readable relative time string (e.g., "2 hours ago", "in 5 minutes").
    /// </summary>
    /// <param name="dateTime">The UTC DateTime to format.</param>
    /// <returns>A human-readable relative time string.</returns>
    public static string GetRelativeTimeString(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime must be UTC", nameof(dateTime));

        var now = GetUtcNowPrecise();
        var diff = now - dateTime;

        // Future time
        if (diff.TotalSeconds < 0)
        {
            diff = diff.Negate();

            if (diff.TotalMinutes < 1)
                return "in a few seconds";
            if (diff.TotalMinutes < 2)
                return "in a minute";
            if (diff.TotalHours < 1)
                return $"in {Math.Floor(diff.TotalMinutes)} minutes";
            if (diff.TotalHours < 2)
                return "in an hour";
            if (diff.TotalDays < 1)
                return $"in {Math.Floor(diff.TotalHours)} hours";
            if (diff.TotalDays < 2)
                return "tomorrow";
            if (diff.TotalDays < 7)
                return $"in {Math.Floor(diff.TotalDays)} days";
            if (diff.TotalDays < 14)
                return "in a week";
            if (diff.TotalDays < 30)
                return $"in {Math.Floor(diff.TotalDays / 7)} weeks";
            if (diff.TotalDays < 60)
                return "in a month";
            if (diff.TotalDays < 365)
                return $"in {Math.Floor(diff.TotalDays / 30)} months";
            return $"in {Math.Floor(diff.TotalDays / 365)} years";
        }

        // Past time
        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalMinutes < 2)
            return "a minute ago";
        if (diff.TotalHours < 1)
            return $"{Math.Floor(diff.TotalMinutes)} minutes ago";
        if (diff.TotalHours < 2)
            return "an hour ago";
        if (diff.TotalDays < 1)
            return $"{Math.Floor(diff.TotalHours)} hours ago";
        if (diff.TotalDays < 2)
            return "yesterday";
        if (diff.TotalDays < 7)
            return $"{Math.Floor(diff.TotalDays)} days ago";
        if (diff.TotalDays < 14)
            return "a week ago";
        if (diff.TotalDays < 30)
            return $"{Math.Floor(diff.TotalDays / 7)} weeks ago";
        if (diff.TotalDays < 60)
            return "a month ago";
        if (diff.TotalDays < 365)
            return $"{Math.Floor(diff.TotalDays / 30)} months ago";
        return $"{Math.Floor(diff.TotalDays / 365)} years ago";
    }

    #endregion Time Formatting
}
