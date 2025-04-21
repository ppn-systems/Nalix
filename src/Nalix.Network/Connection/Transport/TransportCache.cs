using Nalix.Network.Configurations;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Caches;
using Nalix.Shared.Time;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Connection.Transport;

/// <summary>
/// Provides a caching layer for network packets, supporting both outgoing and incoming traffic.
/// </summary>
internal sealed class TransportCache : System.IDisposable
{
    #region Fields

    private static TransportCacheConfig Config => ConfigurationStore.Instance.Get<TransportCacheConfig>();
    private readonly long _startTime = (long)Clock.UnixTime().TotalMilliseconds;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public long Uptime => (long)Clock.UnixTime().TotalMilliseconds - _startTime;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// </summary>
    public long LastPingTime { get; set; }

    /// <summary>
    /// Occurs when a new incoming packet is added to the cache.
    /// </summary>
    public event System.Action? PacketCached;

    /// <summary>
    /// Gets the cache that stores recently sent (outgoing) packets.
    /// </summary>
    public readonly BinaryCache Outgoing = new(Config.Outgoing);

    /// <summary>
    /// Gets the cache that stores recently received (incoming) packets.
    /// </summary>
    public readonly FifoCache<System.ReadOnlyMemory<byte>> Incoming = new(Config.Incoming);

    #endregion

    /// <summary>
    /// Adds a sent packet to the outgoing cache.
    /// A composite key is generated from the first and last 4 bytes of the packet.
    /// </summary>
    /// <param name="data">The packet data to cache.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushOutgoing(System.ReadOnlyMemory<byte> data)
    {
        System.Span<byte> key = stackalloc byte[8];
        data.Span[0..4].CopyTo(key);
        data.Span[^4..].CopyTo(key[4..]);

        Outgoing.Add(key.ToArray(), data);
    }

    /// <summary>
    /// Adds a received packet to the incoming cache and triggers the <see cref="PacketCached"/> event.
    /// </summary>
    /// <param name="data">The received packet data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushIncoming(System.ReadOnlyMemory<byte> data)
    {
        Incoming.Add(data);
        PacketCached?.Invoke();
    }

    /// <summary>
    /// Releases all resources used by this <see cref="TransportCache"/> instance.
    /// Clears and disposes both incoming and outgoing caches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Incoming.Clear();
        Outgoing.Clear();

        Incoming.Dispose();
        Outgoing.Dispose();
    }
}
