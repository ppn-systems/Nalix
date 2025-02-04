using System.Diagnostics;

namespace Notio.Network.Handlers.Base;

internal class PerformanceMonitor
{
    private readonly Stopwatch _stopwatch = new();

    public void Start() => _stopwatch.Restart();

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
}