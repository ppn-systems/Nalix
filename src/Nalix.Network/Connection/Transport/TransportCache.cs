using Nalix.Framework.Time;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Caches;

namespace Nalix.Network.Connection.Transport;

/// <summary>
/// Provides a caching layer for network packets, supporting both outgoing and incoming traffic.
/// </summary>
internal sealed class TransportCache : System.IDisposable
{
    #region Fields

    private static Configurations.NetworkCacheSizeOptions Config
        => ConfigurationStore.Instance.Get<Configurations.NetworkCacheSizeOptions>();

    private readonly System.Int64 _startTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public System.Int64 Uptime => (System.Int64)Clock.UnixTime().TotalMilliseconds - _startTime;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// </summary>
    public System.Int64 LastPingTime { get; set; }

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
    public readonly FifoCache<System.ReadOnlyMemory<System.Byte>> Incoming = new(Config.Incoming);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Adds a sent packet to the outgoing cache.
    /// A composite key is generated from the first and last 4 bytes of the packet.
    /// </summary>
    /// <param name="data">The packet data to cache.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void PushOutgoing(System.ReadOnlyMemory<System.Byte> data)
    {
        System.Span<System.Byte> key = stackalloc System.Byte[8];
        data.Span[0..4].CopyTo(key);
        data.Span[^4..].CopyTo(key[4..]);

        Outgoing.Add(key.ToArray(), data);
    }

    /// <summary>
    /// Adds a received packet to the incoming cache and triggers the <see cref="PacketCached"/> event.
    /// </summary>
    /// <param name="data">The received packet data.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void PushIncoming(System.ReadOnlyMemory<System.Byte> data)
    {
        Incoming.Add(data);
        PacketCached?.Invoke();
    }

    /// <summary>
    /// Releases all resources used by this <see cref="TransportCache"/> instance.
    /// Clears and disposes both incoming and outgoing caches.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Incoming.Clear();
        Outgoing.Clear();

        Incoming.Dispose();
        Outgoing.Dispose();
    }

    #endregion Public Methods
}