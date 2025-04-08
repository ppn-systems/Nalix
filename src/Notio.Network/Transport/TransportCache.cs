using Notio.Network.Configurations;
using Notio.Shared.Configuration;
using Notio.Shared.Memory.Caches;
using Notio.Shared.Time;
using System;

/// <summary>
/// Provides a caching layer for network packets, supporting both outgoing and incoming traffic.
/// </summary>
internal sealed class TransportCache : IDisposable
{
    private static TransportCacheConfig Config => ConfigurationStore.Instance.Get<TransportCacheConfig>();
    private readonly long _connectionStartTime = (long)Clock.UnixTime().TotalMilliseconds;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// </summary>
    public long LastPingTime { get; set; }

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public long Uptime => (long)Clock.UnixTime().TotalMilliseconds - _connectionStartTime;

    /// <summary>
    /// Occurs when a new incoming packet is added to the cache.
    /// </summary>
    public event Action? PacketCached;

    /// <summary>
    /// Gets the cache that stores recently sent (outgoing) packets.
    /// </summary>
    public readonly BinaryCache Outgoing = new(Config.Outgoing);

    /// <summary>
    /// Gets the cache that stores recently received (incoming) packets.
    /// </summary>
    public readonly FifoCache<ReadOnlyMemory<byte>> Incoming = new(Config.Incoming);

    /// <summary>
    /// Adds a sent packet to the outgoing cache.
    /// A composite key is generated from the first and last 4 bytes of the packet.
    /// </summary>
    /// <param name="data">The packet data to cache.</param>
    public void PushOutgoing(ReadOnlyMemory<byte> data)
    {
        Span<byte> key = stackalloc byte[8];
        data.Span[0..4].CopyTo(key);
        data.Span[^4..].CopyTo(key[4..]);

        Outgoing.Add(key.ToArray(), data);
    }

    /// <summary>
    /// Adds a received packet to the incoming cache and triggers the <see cref="PacketCached"/> event.
    /// </summary>
    /// <param name="data">The received packet data.</param>
    public void PushIncoming(ReadOnlyMemory<byte> data)
    {
        Incoming.Add(data);
        PacketCached?.Invoke();
    }

    /// <summary>
    /// Releases all resources used by this <see cref="TransportCache"/> instance.
    /// Clears and disposes both incoming and outgoing caches.
    /// </summary>
    public void Dispose()
    {
        Incoming.Clear();
        Outgoing.Clear();

        Incoming.Dispose();
        Outgoing.Dispose();
    }
}
