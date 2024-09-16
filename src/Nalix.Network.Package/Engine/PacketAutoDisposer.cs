using Nalix.Shared.Time;

namespace Nalix.Network.Package.Engine;

/// <summary>
/// Provides automatic disposal of <see cref="Packet"/> instances after a set lifetime,
/// using a background timer and a concurrent queue (Garbage Collection).
/// </summary>
internal static class PacketAutoDisposer
{
    /// <summary>
    /// Lifetime of each packet in milliseconds before disposal.
    /// </summary>
    private const int LifetimeMs = 60000;

    /// <summary>
    /// Interval in milliseconds to scan the queue for expired packets.
    /// </summary>
    private const int ScanIntervalMs = 5000;

    /// <summary>
    /// Queue storing packets scheduled for disposal.
    /// </summary>
    private static readonly System.Threading.Channels.Channel<Entry> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<Entry>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

    /// <summary>
    /// Timer to periodically scan and clean expired packets.
    /// </summary>
    private static readonly System.Threading.Timer _timer;

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
        => _channel.Writer.TryWrite(new Entry(packet.Timestamp, packet));

    /// <summary>
    /// Processes the queue and disposes packets that have exceeded their lifetime.
    /// </summary>
    /// <param name="_">Unused parameter required by <see cref="System.Threading.TimerCallback"/>.</param>
    private static void ProcessQueue(System.Object? _)
    {
        System.Int64 now = Clock.UnixMillisecondsNow();

        while (_channel.Reader.TryPeek(out Entry entry) &&
               now - entry.Timestamp >= LifetimeMs)
        {
            if (_channel.Reader.TryRead(out Entry expired))
            {
                try
                {
                    expired.Packet.Dispose();
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error disposing packet: " + ex.Message);
                }
            }
        }
    }

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
}