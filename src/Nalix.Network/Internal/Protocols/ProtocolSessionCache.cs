// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Framework.Time;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Caches;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// Provides a caching layer for network packets, supporting both outgoing and incoming traffic.
/// </summary>
internal sealed class ProtocolSessionCache : System.IDisposable
{
    #region Fields

    private IConnection? _sender;
    private IConnectEventArgs? _cachedArgs;
    private System.EventHandler<IConnectEventArgs>? _callback;

    private static Configurations.CacheSizeOptions Config
        => ConfigurationManager.Instance.Get<Configurations.CacheSizeOptions>();

    private readonly System.Int64 _startTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public System.Int64 Uptime => (System.Int64)Clock.UnixTime().TotalMilliseconds - this._startTime;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// </summary>
    public System.Int64 LastPingTime { get; set; }

    /// <summary>
    /// Gets the cache that stores recently received (incoming) packets.
    /// </summary>
    public readonly FifoCache<BufferLease> Incoming;

    #endregion Properties

    #region Constructors

    public ProtocolSessionCache() => this.Incoming = new(Config.Incoming);

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Registers a callback to be invoked when a packet is cached. The state is passed back as the argument.
    /// </summary>
    public void SetCallback(
        System.EventHandler<IConnectEventArgs>? callback,
        IConnection sender,
        IConnectEventArgs args)
    {
        _callback = callback;
        _sender = sender ?? throw new System.ArgumentNullException(nameof(sender));
        _cachedArgs = args ?? throw new System.ArgumentNullException(nameof(args));
    }

    /// <summary>
    /// Adds a received packet to the incoming cache and triggers the cache event.
    /// </summary>
    /// <param name="data">The received packet data.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void PushIncoming(BufferLease data)
    {
        this.Incoming.Push(data);

        // No-alloc hot path: dùng sẵn sender + cached args
        _callback?.Invoke(_sender!, _cachedArgs!);
    }

    /// <summary>
    /// Releases all resources used by this <see cref="ProtocolSessionCache"/> instance.
    /// Clears and disposes both incoming and outgoing caches.
    /// </summary>
    public void Dispose()
    {
        this.Incoming.Clear();
        this.Incoming.Dispose();
    }

    #endregion Public Methods
}