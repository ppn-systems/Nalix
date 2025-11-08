namespace Nalix.Framework.Time;
public static partial class Clock
{
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
}
