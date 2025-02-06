using System.Diagnostics;

namespace Notio.Common.Diagnostics;

public sealed class PerformanceMonitor
{
    private readonly Stopwatch _stopwatch = new();
    private long _elapsedBeforePause = 0;

    public void Start()
    {
        _stopwatch.Restart();
        _elapsedBeforePause = 0;
    }

    public void Pause()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            _elapsedBeforePause += _stopwatch.ElapsedMilliseconds;
        }
    }

    public void Resume()
    {
        if (!_stopwatch.IsRunning)
        {
            _stopwatch.Start();
        }
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public long ElapsedMilliseconds => _elapsedBeforePause + _stopwatch.ElapsedMilliseconds;
    public double ElapsedSeconds => ElapsedMilliseconds / 1000.0;
    public long ElapsedTicks => _stopwatch.ElapsedTicks;
}