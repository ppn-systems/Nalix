using System;
using System.Diagnostics;

namespace Notio.Diagnostics;

/// <summary>
/// A utility class for measuring elapsed time with the ability to start, pause, resume, and stop the timer.
/// Implements IDisposable for proper resource cleanup.
/// </summary>
public sealed class PerformanceMonitor : IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private long _elapsedBeforePause;
    private bool _isPaused;
    private bool _isDisposed;

    /// <summary>
    /// Starts or restarts the timer.
    /// </summary>
    public void Start()
    {
        ThrowIfDisposed();
        _stopwatch.Restart();
        _elapsedBeforePause = 0;
        _isPaused = false;
    }

    /// <summary>
    /// Pauses the timer and accumulates elapsed time.
    /// </summary>
    public void Pause()
    {
        ThrowIfDisposed();
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            _elapsedBeforePause += _stopwatch.ElapsedMilliseconds;
            _isPaused = true;
        }
    }

    /// <summary>
    /// Resumes the timer from a paused state.
    /// </summary>
    public void Resume()
    {
        ThrowIfDisposed();
        if (_isPaused)
        {
            _stopwatch.Start();
            _isPaused = false;
        }
    }

    /// <summary>
    /// Stops the timer completely.
    /// </summary>
    public void Stop()
    {
        ThrowIfDisposed();
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            _elapsedBeforePause += _stopwatch.ElapsedMilliseconds;
        }
    }

    /// <summary>
    /// Gets the total elapsed time in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds
    {
        get
        {
            ThrowIfDisposed();
            return _elapsedBeforePause + (_stopwatch.IsRunning ? _stopwatch.ElapsedMilliseconds : 0);
        }
    }

    /// <summary>
    /// Gets the total elapsed time in seconds.
    /// </summary>
    public double ElapsedSeconds => ElapsedMilliseconds / 1000.0;

    /// <summary>
    /// Gets the total number of ticks elapsed.
    /// </summary>
    public long ElapsedTicks => _stopwatch.ElapsedTicks;

    /// <summary>
    /// Disposes the performance monitor and releases its resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _stopwatch.Stop();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PerformanceMonitor), "Cannot use a disposed PerformanceMonitor.");
        }
    }
}
