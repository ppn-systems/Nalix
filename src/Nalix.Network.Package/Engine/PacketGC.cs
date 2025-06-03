using Nalix.Shared.Time;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// PacketGC siêu nhẹ: tự động Dispose các Packet sau một khoảng thời gian.
/// </summary>
internal static class PacketGC
{
    // Dùng struct để tránh boxing, giảm pressure GC so với tuple
    private readonly struct Entry(long ts, Packet pkt)
    {
        public readonly long Timestamp = ts;
        public readonly Packet Packet = pkt;
    }

    private static readonly System.Collections.Concurrent.ConcurrentQueue<Entry> _queue = new();
    private static readonly System.Threading.Timer _timer;
    private const int LifetimeMs = 60000;
    private const int ScanIntervalMs = 5000;

    // Dùng static readonly để tránh delegate allocation mỗi lần quét
    private static readonly System.Threading.TimerCallback _timerCallback = ProcessQueue;

    static PacketGC()
    {
        _timer = new System.Threading.Timer(_timerCallback, null, ScanIntervalMs, ScanIntervalMs);
    }

    /// <summary>
    /// Đăng ký Packet vào GC, sẽ tự động Dispose sau 5s.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void Register(Packet packet)
        => _queue.Enqueue(new Entry(Clock.UnixMillisecondsNow(), packet));

    // Không public, chỉ dùng cho timer
    private static void ProcessQueue(System.Object? _)
    {
        if (_queue.IsEmpty) return;

        while (_queue.TryPeek(out Entry item) &&
               Clock.UnixMillisecondsNow() - item.Timestamp >= LifetimeMs)
        {
            if (_queue.TryDequeue(out Entry expired))
            {
                try { expired.Packet.Dispose(); } catch { }
            }
        }
    }
}
