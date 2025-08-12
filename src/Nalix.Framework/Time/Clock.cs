namespace Nalix.Framework.Time;

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

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the system clock is using high-resolution timing.
    /// </summary>
    public static System.Boolean IsHighResolution { get; private set; }

    /// <summary>
    /// Gets the tick frequency in seconds (the duration of a single tick).
    /// </summary>
    public static System.Double TickFrequency { get; private set; }

    /// <summary>
    /// Gets the frequency of the high-resolution timer in ticks per second.
    /// </summary>
    public static System.Int64 TicksPerSecond => System.Diagnostics.Stopwatch.Frequency;

    /// <summary>
    /// Gets a value indicating whether the clock has been synchronized with an external time source.
    /// </summary>
    public static System.Boolean IsSynchronized { get; private set; }

    /// <summary>
    /// Gets the time when the last synchronization occurred.
    /// </summary>
    public static System.DateTime LastSyncTime { get; private set; }

    /// <summary>
    /// Gets the current offset in milliseconds between the system time and the synchronized time.
    /// </summary>
    public static System.Double CurrentOffsetMs
        => System.TimeSpan.FromTicks(_timeOffset).TotalMilliseconds;

    #endregion Properties

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

    #region Basic Time Functions

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

    #endregion Basic Time Functions

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
    public static System.Double SynchronizeTime(
        System.DateTime externalTime,
        System.Double maxAllowedDriftMs = 1000.0)
    {
        if (externalTime.Kind != System.DateTimeKind.Utc)
        {
            throw new System.ArgumentException("External time must be UTC", nameof(externalTime));
        }

        // Calculate current time according to our clock
        System.DateTime currentTime = GetUtcNowPrecise();

        // Calculate time difference
        var timeDifference = externalTime - currentTime;

        // Only adjust if drift is above threshold
        if (System.Math.Abs(timeDifference.TotalMilliseconds) <= maxAllowedDriftMs)
        {
            return 0;
        }

        // Calculate and apply offset
        _timeOffset = timeDifference.Ticks;
        IsSynchronized = true;
        LastSyncTime = externalTime;

        // Calculate drift rate if we have previous sync data
        if (LastSyncTime != System.DateTime.MinValue)
        {
            var elapsedSinceLastSync = (externalTime - LastSyncTime).TotalSeconds;
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
    /// Gets the elapsed time since the last call to StartMeasurement() for the current thread.
    /// </summary>
    /// <returns>The elapsed time as a TimeSpan.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan GetElapsed() => _threadStopwatch.Value!.Elapsed;

    /// <summary>
    /// Gets the elapsed time in milliseconds since the last call to StartMeasurement() for the current thread.
    /// </summary>
    /// <returns>The elapsed time in milliseconds.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Double GetElapsedMilliseconds()
        => _threadStopwatch.Value!.Elapsed.TotalMilliseconds;

    /// <summary>
    /// Gets the elapsed time in microseconds since the last call to StartMeasurement() for the current thread.
    /// </summary>
    /// <returns>The elapsed time in microseconds.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Double GetElapsedMicroseconds()
        => _threadStopwatch.Value!.Elapsed.Ticks / 10.0;

    /// <summary>
    /// Creates a new TimeStamp for precise interval measurement.
    /// </summary>
    /// <returns>A TimeStamp object representing the current time.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TimeStamp CreateTimeStamp() => new(System.Diagnostics.Stopwatch.GetTimestamp());

    /// <summary>
    /// Measures the execution time of an action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    public static System.Double MeasureExecutionTime(System.Action action)
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
    public static (T Result, System.Double ElapsedMs) MeasureFunction<T>(System.Func<T> func)
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
    public static System.Boolean WaitUntil(
        System.DateTime targetTime,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (targetTime.Kind != System.DateTimeKind.Utc)
        {
            throw new System.ArgumentException("Target time must be UTC", nameof(targetTime));
        }

        while (GetUtcNowPrecise() < targetTime)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            // Calculate how long to sleep
            var remainingTime = targetTime - GetUtcNowPrecise();
            if (remainingTime <= System.TimeSpan.Zero)
            {
                break;
            }

            // For longer waits, sleep in chunks to allow cancellation checks
            if (remainingTime.TotalMilliseconds > 50)
            {
                System.Threading.Thread.Sleep(System.Math.Min((System.Int32)remainingTime.TotalMilliseconds / 2, 50));
            }
            else if (remainingTime.TotalMilliseconds > 1)
            {
                System.Threading.Thread.Sleep(1);
            }
            else
            {
                System.Threading.Thread.SpinWait(100); // For sub-millisecond precision
            }
        }

        return !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Returns true if the current time is between the start and end times.
    /// </summary>
    /// <param name="startTime">The start time (inclusive).</param>
    /// <param name="endTime">The end time (exclusive).</param>
    /// <returns>True if the current time is within the specified range.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsTimeBetween(
        System.DateTime startTime,
        System.DateTime endTime)
    {
        var now = GetUtcNowPrecise();
        return now >= startTime && now < endTime;
    }

    /// <summary>
    /// Calculates the time remaining until a specified time.
    /// </summary>
    /// <param name="targetTime">The target UTC time.</param>
    /// <returns>The TimeSpan until the target time, or TimeSpan.Zero if the target time has passed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.TimeSpan GetTimeRemaining(System.DateTime targetTime)
    {
        var timeRemaining = targetTime - GetUtcNowPrecise();
        return timeRemaining > System.TimeSpan.Zero ? timeRemaining : System.TimeSpan.Zero;
    }

    /// <summary>
    /// Determines if a given time has elapsed since a start time.
    /// </summary>
    /// <param name="startTime">The start time (UTC).</param>
    /// <param name="duration">The duration to check.</param>
    /// <returns>True if the duration has elapsed since the start time.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean HasElapsed(
        System.DateTime startTime,
        System.TimeSpan duration)
        => GetUtcNowPrecise() - startTime >= duration;

    /// <summary>
    /// Gets the percentage of completion between a start time and an end time.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <returns>A value between 0.0 and 1.0 representing the percentage completion.</returns>
    public static System.Double GetPercentageComplete(
        System.DateTime startTime,
        System.DateTime endTime)
    {
        if (startTime >= endTime)
        {
            return 1.0;
        }

        var totalDuration = endTime - startTime;
        var elapsed = GetUtcNowPrecise() - startTime;

        return elapsed <= System.TimeSpan.Zero
            ? 0.0
            : elapsed >= totalDuration ? 1.0 : elapsed.TotalMilliseconds / totalDuration.TotalMilliseconds;
    }

    #endregion Time-based Operations

    #region Time Formatting

    /// <summary>
    /// Formats a TimeSpan as a human-readable string (e.g., "2d 5h 30m 15s").
    /// </summary>
    /// <param name="timeSpan">The TimeSpan to format.</param>
    /// <param name="includeMilliseconds">Whether to include milliseconds in the output.</param>
    /// <returns>A formatted string representation of the TimeSpan.</returns>
    public static System.String FormatTimeSpan(System.TimeSpan timeSpan, System.Boolean includeMilliseconds = false)
    {
        if (timeSpan == System.TimeSpan.Zero)
        {
            return "0s";
        }

        System.Collections.Generic.List<System.String> parts = [];

        if (timeSpan.Days > 0)
        {
            parts.Add($"{timeSpan.Days}d");
        }

        if (timeSpan.Hours > 0)
        {
            parts.Add($"{timeSpan.Hours}h");
        }

        if (timeSpan.Minutes > 0)
        {
            parts.Add($"{timeSpan.Minutes}m");
        }

        if (timeSpan.Seconds > 0 || (parts.Count == 0 && !includeMilliseconds))
        {
            parts.Add($"{timeSpan.Seconds}s");
        }

        if (includeMilliseconds && (timeSpan.Milliseconds > 0 || parts.Count == 0))
        {
            parts.Add($"{timeSpan.Milliseconds}ms");
        }

        return System.String.Join(" ", parts);
    }

    /// <summary>
    /// Formats the time elapsed since a specified UTC DateTime as a human-readable string.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="includeMilliseconds">Whether to include milliseconds in the output.</param>
    /// <returns>A formatted string representing the elapsed time.</returns>
    public static System.String FormatElapsedTime(System.DateTime startTime, System.Boolean includeMilliseconds = false)
    {
        return startTime.Kind != System.DateTimeKind.Utc
            ? throw new System.ArgumentException("Start time must be UTC", nameof(startTime))
            : FormatTimeSpan(GetUtcNowPrecise() - startTime, includeMilliseconds);
    }

    #endregion Time Formatting
}
