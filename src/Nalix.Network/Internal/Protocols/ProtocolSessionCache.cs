// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Time;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Caches;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Provides a caching layer for network packets, supporting both outgoing and incoming traffic.
/// </summary>
internal sealed class ProtocolSessionCache : System.IDisposable
{
    #region Fields

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
    /// Occurs when a new incoming packet is added to the cache.
    /// </summary>
    public event System.Action? PacketCached;

    /// <summary>
    /// Gets the cache that stores recently received (incoming) packets.
    /// </summary>
    public readonly FifoCache<System.ReadOnlyMemory<System.Byte>> Incoming;

    #endregion Properties

    #region Constructors

    public ProtocolSessionCache() => this.Incoming = new(Config.Incoming);

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Adds a received packet to the incoming cache and triggers the <see cref="PacketCached"/> event.
    /// </summary>
    /// <param name="data">The received packet data.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void PushIncoming(System.ReadOnlyMemory<System.Byte> data)
    {
        this.Incoming.Push(data);
        PacketCached?.Invoke();
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

    #region Private Methods

    internal static unsafe System.UInt64 HashToUInt64(System.ReadOnlySpan<System.Byte> data)
    {
        const System.Int32 r = 47;
        const System.UInt64 s = 0xc70f6907UL;
        const System.UInt64 m = 0xc6a4a7935bd1e995UL;

        System.UInt64 h = s ^ (System.UInt64)data.Length * m;

        fixed (System.Byte* pBase = data)
        {
            System.Byte* p = pBase;
            System.Int32 len = data.Length;

            // body
            while (len >= 8)
            {
                // unaligned read
                System.UInt64 k = *(System.UInt64*)p;
                if (!System.BitConverter.IsLittleEndian)
                {
                    k = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(k);
                }

                k *= m;
                k ^= k >> r;
                k *= m;

                h ^= k;
                h *= m;

                p += 8;
                len -= 8;
            }

            // tail (0..7 bytes), pack little-endian without alloc
            if (len > 0)
            {
                System.UInt64 k = 0;
                for (System.Int32 i = 0; i < len; i++)
                {
                    k |= (System.UInt64)p[i] << 8 * i;
                }

                h ^= k;
                h *= m;
            }
        }

        // finalization
        h ^= h >> r;
        h *= m;
        h ^= h >> r;

        return h;
    }

    #endregion Private Methods
}