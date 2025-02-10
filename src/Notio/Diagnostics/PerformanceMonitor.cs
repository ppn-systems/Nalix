using System.Diagnostics;

namespace Notio.Diagnostics;

/// <summary>
/// A utility class for measuring elapsed time with the ability to start, pause, resume, and stop the timer.
/// </summary>
/// <remarks>
/// This class is useful for performance monitoring, where you need to track time intervals across multiple stages
/// and potentially pause and resume the measurement without resetting the elapsed time.
/// </remarks>
public sealed class PerformanceMonitor
{
    private readonly Stopwatch _stopwatch = new();
    private long _elapsedBeforePause = 0;

    /// <summary>
    /// Starts the timer or restarts it if it was already running.
    /// </summary>
    public void Start()
    {
        _stopwatch.Restart();
        _elapsedBeforePause = 0;
    }

    /// <summary>
    /// Pauses the timer and accumulates the elapsed time before the pause.
    /// </summary>
    public void Pause()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            _elapsedBeforePause += _stopwatch.ElapsedMilliseconds;
        }
    }

    /// <summary>
    /// Resumes the timer from its paused state.
    /// </summary>
    public void Resume()
    {
        if (!_stopwatch.IsRunning)
        {
            _stopwatch.Start();
        }
    }

    /// <summary>
    /// Stops the timer completely, and prevents any further measurements.
    /// </summary>
    public void Stop() => _stopwatch.Stop();

    /// <summary>
    /// Gets the total elapsed time in milliseconds, including time before any pause.
    /// </summary>
    /// <value>
    /// The total elapsed time in milliseconds.
    /// </value>
    public long ElapsedMilliseconds => _elapsedBeforePause + _stopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Gets the total elapsed time in seconds, including time before any pause.
    /// </summary>
    /// <value>
    /// The total elapsed time in seconds.
    /// </value>
    public double ElapsedSeconds => ElapsedMilliseconds / 1000.0;

    /// <summary>
    /// Gets the total number of ticks (time intervals) that have passed since the timer started or was last reset.
    /// </summary>
    /// <value>
    /// The total number of ticks.
    /// </value>
    public long ElapsedTicks => _stopwatch.ElapsedTicks;
}
