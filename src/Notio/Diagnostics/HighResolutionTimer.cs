namespace Notio.Diagnostics;

/// <summary>
/// Provides access to a high-resolution, time measuring device.
/// </summary>
/// <seealso cref="System.Diagnostics.Stopwatch" />
public sealed class HighResolutionTimer : System.Diagnostics.Stopwatch
{
    /// <summary>
    /// Gets the Number of microseconds per timer tick.
    /// </summary>
    private static double MicrosecondsPerTick { get; } = 1000000d / Frequency;

    /// <summary>
    /// Gets the elapsed microseconds.
    /// </summary>
    public long ElapsedMicroseconds => (long)(ElapsedTicks * MicrosecondsPerTick);

    /// <summary>
    /// Initializes a new instance of the <see cref="HighResolutionTimer"/> class.
    /// </summary>
    /// <exception cref="System.NotSupportedException">High-resolution timer not available.</exception>
    public HighResolutionTimer()
    {
        if (!IsHighResolution)
            throw new System.NotSupportedException("High-resolution timer not available");
    }
}
