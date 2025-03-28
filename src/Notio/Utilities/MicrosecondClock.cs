using Notio.Defaults;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Notio.Utilities;

/// <summary>
/// Provides a high-resolution timestamp in microseconds.
/// </summary>
public static class MicrosecondClock
{
    /// <summary>
    /// Conversion factor from stopwatch ticks to microseconds.
    /// </summary>
    public static readonly double TickMicroseconds =
        (DefaultConstants.MicrosecondsInSecond / Stopwatch.Frequency);

    /// <summary>
    /// Gets the current timestamp in microseconds.
    /// </summary>
    /// <returns>The number of microseconds elapsed since an arbitrary point in time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetTimestamp()
        => (ulong)(Stopwatch.GetTimestamp() * TickMicroseconds);
}
