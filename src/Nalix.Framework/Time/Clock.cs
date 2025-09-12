// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Time;

/// <summary>
/// Handles precise time for the system with high accuracy, supporting various time-related operations
/// required for real-time communication and distributed systems.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
public static partial class Clock
{
    #region Constants and Fields

    /// <summary>
    /// The Unix timestamp representing the start of 2020 (Wed Jan 01 2020 00:00:00 UTC).
    /// </summary>
    public const System.Int64 TimeEpochTimestamp = 1577836800L; // (Wed Jan 01 2020 00:00:00)

    /// <summary>
    /// The <see cref="System.DateTime"/> representation of the start of 2020 (Wed Jan 01 2020 00:00:00 UTC).
    /// </summary>
    public static readonly System.DateTime TimeEpochDatetime;

    // BaseValue36 values for high-precision time calculation

    private static readonly System.DateTime UtcBase;
    private static readonly System.DateTime TimeEpoch;
    private static readonly System.Diagnostics.Stopwatch UtcStopwatch;

    // Time synchronization variables

    private static System.Int64 _timeOffset;
    private static System.Double _driftCorrection;

    // Performance measurement fields
    private static readonly System.Threading.ThreadLocal<System.Diagnostics.Stopwatch> _threadStopwatch;

    #endregion Constants and Fields

    #region Constructors

    static Clock()
    {
        // Static class, no instantiation allowed

        UtcBase = System.DateTime.UtcNow;
        UtcStopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimeEpoch = System.DateTime.UnixEpoch.AddSeconds(TimeEpochTimestamp);
        TimeEpochDatetime = new(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

        _timeOffset = 0; // In ticks, adjusted from external time sources
        _driftCorrection = 1.0; // Multiplier to correct for system clock drift
        IsSynchronized = false;
        LastSyncTime = System.DateTime.MinValue;

        TickFrequency = 1.0 / System.Diagnostics.Stopwatch.Frequency;
        IsHighResolution = System.Diagnostics.Stopwatch.IsHighResolution;
        _threadStopwatch = new(System.Diagnostics.Stopwatch.StartNew);
    }

    #endregion Constructors

    #region Conversion Methods

    /// <summary>
    /// Converts Unix timestamp (seconds) to DateTime with overflow check.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime UnixTimeSecondsToDateTime(System.Int64 timestamp)
    {
        return timestamp > (System.Int64)System.DateTime.MaxValue.Subtract(System.DateTime.UnixEpoch).TotalSeconds
            ? throw new System.OverflowException("Timestamp exceeds DateTime limits")
            : System.DateTime.UnixEpoch.AddSeconds(timestamp);
    }

    /// <summary>
    /// Converts Unix timestamp (milliseconds) to DateTime with overflow check.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime UnixTimeMillisecondsToDateTime(System.Int64 timestamp)
    {
        return timestamp > (System.Int64)System.DateTime.MaxValue.Subtract(System.DateTime.UnixEpoch).TotalMilliseconds
            ? throw new System.OverflowException("Timestamp exceeds DateTime limits")
            : System.DateTime.UnixEpoch.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// Converts Unix timestamp (microseconds) to DateTime with overflow check.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime UnixTimeMicrosecondsToDateTime(System.Int64 timestamp)
    {
        return timestamp > System.DateTime.MaxValue.Subtract(System.DateTime.UnixEpoch).Ticks / 10
            ? throw new System.OverflowException("Timestamp exceeds DateTime limits")
            : System.DateTime.UnixEpoch.AddTicks(timestamp * 10);
    }

    /// <summary>
    /// Converts timestamp (milliseconds) to DateTime with overflow check.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime TimeMillisecondsToDateTime(System.Int64 timestamp)
    {
        return timestamp > (System.Int64)System.DateTime.MaxValue.Subtract(TimeEpoch).TotalMilliseconds
            ? throw new System.OverflowException("Timestamp exceeds DateTime limits")
            : TimeEpoch.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// Converts TimeSpan to DateTime with validation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime UnixTimeToDateTime(System.TimeSpan timeSpan)
    {
        return timeSpan.Ticks < 0
            ? throw new System.ArgumentException(
                "TimeSpan cannot be negative", nameof(timeSpan))
            : System.DateTime.UnixEpoch.Add(timeSpan);
    }

    /// <summary>
    /// Converts DateTime to Unix TimeSpan with validation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan DateTimeToUnixTime(System.DateTime dateTime)
    {
        return dateTime.Kind != System.DateTimeKind.Utc
            ? throw new System.ArgumentException(
                "DateTime must be UTC", nameof(dateTime))
            : dateTime - System.DateTime.UnixEpoch;
    }

    /// <summary>
    /// Converts DateTime to application-specific TimeSpan with validation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan DateTimeToTime(System.DateTime dateTime)
    {
        return dateTime.Kind != System.DateTimeKind.Utc
            ? throw new System.ArgumentException("DateTime must be UTC", nameof(dateTime))
            : dateTime - TimeEpoch;
    }

    /// <summary>
    /// Converts UTC DateTime to Unix timestamp in seconds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt64 DateTimeToUnixTimeSeconds(System.DateTime utcDateTime)
    {
        return utcDateTime.Kind != System.DateTimeKind.Utc
            ? throw new System.ArgumentException(
                "DateTime must be UTC", nameof(utcDateTime))
            : (System.UInt64)(utcDateTime - System.DateTime.UnixEpoch).TotalSeconds;
    }

    /// <summary>
    /// Converts UTC DateTime to Unix timestamp in milliseconds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.UInt64 DateTimeToUnixTimeMilliseconds(System.DateTime utcDateTime)
    {
        return utcDateTime.Kind != System.DateTimeKind.Utc
            ? throw new System.ArgumentException(
                "DateTime must be UTC", nameof(utcDateTime))
            : (System.UInt64)(utcDateTime - System.DateTime.UnixEpoch).TotalMilliseconds;
    }

    #endregion Conversion Methods

    #region Comparison Methods

    /// <summary>
    /// Compares two TimeSpan values and returns the greater one.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan Max(
        System.TimeSpan time1,
        System.TimeSpan time2)
        => time1 > time2 ? time1 : time2;

    /// <summary>
    /// Compares two TimeSpan values and returns the lesser one.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan Min(
        System.TimeSpan time1,
        System.TimeSpan time2)
        => time1 < time2 ? time1 : time2;

    /// <summary>
    /// Checks if a DateTime is within the allowed range.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsInRange(
        System.DateTime dateTime,
        System.TimeSpan range)
    {
        System.TimeSpan diff = GetUtcNowPrecise() - dateTime;
        return diff.Ticks >= -range.Ticks && diff.Ticks <= range.Ticks;
    }

    /// <summary>
    /// Clamps a DateTime between a minimum and maximum value.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.DateTime Clamp(
        System.DateTime value,
        System.DateTime min,
        System.DateTime max) => value < min ? min : value > max ? max : value;

    /// <summary>
    /// Clamps a TimeSpan between a minimum and maximum value.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan Clamp(
        System.TimeSpan value,
        System.TimeSpan min,
        System.TimeSpan max) => value < min ? min : value > max ? max : value;

    #endregion Comparison Methods

    #region Time Synchronization Methods

    /// <summary>
    /// Synchronizes the clock with an external time source.
    /// </summary>
    /// <param name="externalTime">The accurate external UTC time.</param>
    /// <param name="maxAllowedDriftMs">Maximum allowed drift in milliseconds before adjustment is applied.</param>
    /// <returns>The adjustment made in milliseconds.</returns>
    public static System.Double SynchronizeTime(System.DateTime externalTime, System.Double maxAllowedDriftMs = 1000.0)
    {
        if (externalTime.Kind != System.DateTimeKind.Utc)
        {
            throw new System.ArgumentException("External time must be UTC", nameof(externalTime));
        }

        var currentTime = GetUtcNowPrecise();
        var timeDifference = externalTime - currentTime;
        if (System.Math.Abs(timeDifference.TotalMilliseconds) <= maxAllowedDriftMs)
        {
            return 0;
        }

        // capture previous before overwrite
        var previousSync = LastSyncTime;

        _timeOffset = timeDifference.Ticks;
        IsSynchronized = true;
        LastSyncTime = externalTime;

        if (previousSync != System.DateTime.MinValue)
        {
            var elapsedSinceLastSync = (externalTime - previousSync).TotalSeconds;
            if (elapsedSinceLastSync > 60)
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
        IsSynchronized = false;
        LastSyncTime = System.DateTime.MinValue;
    }

    /// <summary>
    /// Gets the estimated clock drift rate.
    /// A value greater than 1.0 means the local clock is running slower than the reference clock.
    /// A value less than 1.0 means the local clock is running faster than the reference clock.
    /// </summary>
    public static System.Double GetDriftRate() => _driftCorrection;

    /// <summary>
    /// Gets the current error estimate between the synchronized time and system time in milliseconds.
    /// </summary>
    public static System.Double GetCurrentErrorEstimateMs()
    {
        if (!IsSynchronized)
        {
            return 0;
        }

        var driftedTime = GetUtcNowPrecise();
        var systemTime = System.DateTime.UtcNow;
        return (driftedTime - systemTime).TotalMilliseconds;
    }

    #endregion Time Synchronization Methods

    #region Performance Measurement

    /// <summary>
    /// Starts a new performance measurement using the current thread's stopwatch.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void StartMeasurement() => _threadStopwatch.Value!.Restart();

    /// <summary>
    /// Gets the elapsed time in milliseconds since the last call to StartMeasurement() for the current thread.
    /// </summary>
    /// <returns>The elapsed time in milliseconds.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Double GetElapsedMilliseconds()
        => _threadStopwatch.Value!.Elapsed.TotalMilliseconds;

    #endregion Performance Measurement
}
