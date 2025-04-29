using Nalix.Environment;

namespace Nalix.Utils;

/// <summary>
/// Provides a high-resolution timestamp in microseconds.
/// </summary>
public static class PreciseTimeClock
{
    /// <summary>
    /// Conversion factor from stopwatch ticks to microseconds.
    /// </summary>
    public static readonly double TickMicroseconds =
        (Performance.MicrosecondsInSecond / System.Diagnostics.Stopwatch.Frequency);

    /// <summary>
    /// Gets the current timestamp in microseconds.
    /// </summary>
    /// <returns>The Number of microseconds elapsed since an arbitrary point in time.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ulong GetTimestamp()
        => (ulong)(System.Diagnostics.Stopwatch.GetTimestamp() * TickMicroseconds);
}
