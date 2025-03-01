using System;
using System.Diagnostics;

namespace Notio.Shared.Time;

/// <summary>
/// Represents a precise timestamp for interval measurement.
/// </summary>
public readonly struct TimeStamp : IEquatable<TimeStamp>, IComparable<TimeStamp>
{
    private readonly long _timestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeStamp"/> struct.
    /// </summary>
    /// <param name="timestamp">The raw timestamp value.</param>
    internal TimeStamp(long timestamp) => _timestamp = timestamp;

    /// <summary>
    /// Gets the elapsed time since this timestamp was created.
    /// </summary>
    /// <returns>A TimeSpan representing the elapsed interval.</returns>
    public TimeSpan GetElapsed()
    {
        var elapsed = Stopwatch.GetTimestamp() - _timestamp;
        return TimeSpan.FromSeconds(elapsed * Clock.TickFrequency);
    }

    /// <summary>
    /// Gets the elapsed milliseconds since this timestamp was created.
    /// </summary>
    /// <returns>The elapsed milliseconds.</returns>
    public double GetElapsedMilliseconds()
    {
        var elapsed = Stopwatch.GetTimestamp() - _timestamp;
        return elapsed * Clock.TickFrequency * 1000.0;
    }

    /// <summary>
    /// Gets the elapsed microseconds since this timestamp was created.
    /// </summary>
    /// <returns>The elapsed microseconds.</returns>
    public double GetElapsedMicroseconds()
    {
        var elapsed = Stopwatch.GetTimestamp() - _timestamp;
        return elapsed * Clock.TickFrequency * 1000000.0;
    }

    /// <summary>
    /// Gets the raw timestamp value.
    /// </summary>
    public long RawValue => _timestamp;

    /// <summary>
    /// Calculates the interval between two timestamps.
    /// </summary>
    /// <param name="start">The start timestamp.</param>
    /// <param name="end">The end timestamp.</param>
    /// <returns>A TimeSpan representing the interval.</returns>
    public static TimeSpan GetInterval(TimeStamp start, TimeStamp end)
    {
        var ticks = end._timestamp - start._timestamp;
        return TimeSpan.FromSeconds(ticks * Clock.TickFrequency);
    }

    /// <summary>
    /// Calculates the interval between two timestamps in milliseconds.
    /// </summary>
    /// <param name="start">The start timestamp.</param>
    /// <param name="end">The end timestamp.</param>
    /// <returns>The interval in milliseconds.</returns>
    public static double GetIntervalMilliseconds(TimeStamp start, TimeStamp end)
    {
        var ticks = end._timestamp - start._timestamp;
        return ticks * Clock.TickFrequency * 1000.0;
    }

    /// <summary>
    /// Calculates the interval between two timestamps in microseconds.
    /// </summary>
    /// <param name="start">The start timestamp.</param>
    /// <param name="end">The end timestamp.</param>
    /// <returns>The interval in microseconds.</returns>
    public static double GetIntervalMicroseconds(TimeStamp start, TimeStamp end)
    {
        var ticks = end._timestamp - start._timestamp;
        return ticks * Clock.TickFrequency * 1000000.0;
    }

    /// <summary>
    /// Gets the current timestamp.
    /// </summary>
    public static TimeStamp Now => new(Stopwatch.GetTimestamp());

    /// <inheritdoc/>
    public bool Equals(TimeStamp other) => _timestamp == other._timestamp;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is TimeStamp stamp && Equals(stamp);

    /// <inheritdoc/>
    public override int GetHashCode() => _timestamp.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(TimeStamp other)
        => _timestamp.CompareTo(other._timestamp);

    /// <summary>
    /// Checks if two timestamps are equal.
    /// </summary>
    public static bool operator ==(TimeStamp left, TimeStamp right)
        => left.Equals(right);

    /// <summary>
    /// Checks if two timestamps are not equal.
    /// </summary>
    public static bool operator !=(TimeStamp left, TimeStamp right)
        => !left.Equals(right);

    /// <summary>
    /// Checks if the left timestamp is less than the right timestamp.
    /// </summary>
    public static bool operator <(TimeStamp left, TimeStamp right)
        => left._timestamp < right._timestamp;

    /// <summary>
    /// Checks if the left timestamp is greater than the right timestamp.
    /// </summary>
    public static bool operator >(TimeStamp left, TimeStamp right)
        => left._timestamp > right._timestamp;

    /// <summary>
    /// Checks if the left timestamp is less than or equal to the right timestamp.
    /// </summary>
    public static bool operator <=(TimeStamp left, TimeStamp right)
        => left._timestamp <= right._timestamp;

    /// <summary>
    /// Checks if the left timestamp is greater than or equal to the right timestamp.
    /// </summary>
    public static bool operator >=(TimeStamp left, TimeStamp right)
        => left._timestamp >= right._timestamp;

    /// <summary>
    /// Returns a string representation of this timestamp.
    /// </summary>
    public override string ToString() => $"TimeStamp({_timestamp})";
}
