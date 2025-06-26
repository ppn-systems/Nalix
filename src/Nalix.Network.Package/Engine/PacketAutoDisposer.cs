using Nalix.Shared.Time;
using System;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// Provides automatic disposal of <see cref="Packet"/> instances after a set lifetime,
/// using a background timer and a concurrent queue (Garbage Collection).
/// </summary>
internal static class PacketAutoDisposer
{
    /// <summary>
    /// Represents a packet and the timestamp it was registered at.
    /// </summary>
    private readonly struct Entry(long ts, Packet pkt)
    {
        /// <summary>
        /// Gets the timestamp in Unix milliseconds when the packet was added to the queue.
        /// </summary>
        public readonly long Timestamp = ts;

        /// <summary>
        /// Gets the packet instance to be disposed.
        /// </summary>
        public readonly Packet Packet = pkt;
    }

    /// <summary>
    /// Queue storing packets scheduled for disposal.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentQueue<Entry> _queue = new();

    /// <summary>
    /// Timer to periodically scan and clean expired packets.
    /// </summary>
    private static readonly System.Threading.Timer _timer;

    /// <summary>
    /// Lifetime of each packet in milliseconds before disposal.
    /// </summary>
    private const int LifetimeMs = 60000;

    /// <summary>
    /// Interval in milliseconds to scan the queue for expired packets.
    /// </summary>
    private const int ScanIntervalMs = 5000;

    /// <summary>
    /// Callback method used by the timer to process the packet queue.
    /// </summary>
    private static readonly System.Threading.TimerCallback _timerCallback = ProcessQueue;

    /// <summary>
    /// Initializes the <see cref="PacketAutoDisposer"/> by starting the periodic cleanup timer.
    /// </summary>
    static PacketAutoDisposer()
    {
        _timer = new System.Threading.Timer(_timerCallback, null, ScanIntervalMs, ScanIntervalMs);
    }

    /// <summary>
    /// Registers a <see cref="Packet"/> instance to be automatically disposed after a timeout.
    /// </summary>
    /// <param name="packet">The packet to be tracked and disposed.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void Register(Packet packet)
        => _queue.Enqueue(new Entry(Clock.UnixMillisecondsNow(), packet));

    /// <summary>
    /// Processes the queue and disposes packets that have exceeded their lifetime.
    /// </summary>
    /// <param name="_">Unused parameter required by <see cref="System.Threading.TimerCallback"/>.</param>
    private static void ProcessQueue(System.Object? _)
    {
        if (_queue.IsEmpty) return;

        while (_queue.TryPeek(out Entry item) &&
               Clock.UnixMillisecondsNow() - item.Timestamp >= LifetimeMs)
        {
            if (_queue.TryDequeue(out Entry expired))
            {
                try
                {
                    expired.Packet.Dispose();
                }
                catch (Exception ex)
                {
                    // Ghi log lỗi thay vì bỏ qua
                    System.Diagnostics.Debug.WriteLine("Error disposing packet: " + ex.Message);
                }
            }
        }
    }
}
