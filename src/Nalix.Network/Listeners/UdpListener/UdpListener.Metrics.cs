// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    #region Nested Metrics Class

    /// <summary>
    /// Metrics for tracking UDP listener datagram lifecycle and drops.
    /// Lock-free, thread-safe, zero-allocation design using atomic operations.
    /// </summary>
    public sealed class UMetrics
    {
        #region Fields

        private long _rxPackets;
        private long _rxBytes;
        private long _dropShort;
        private long _dropUnauth;
        private long _dropUnknown;
        private long _dropRateLimited;
        private long _dropOversize;
        private long _recvErrors;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the total number of received packets.
        /// </summary>
        public long ReceivedPackets => Volatile.Read(ref _rxPackets);

        /// <summary>
        /// Gets the total number of received bytes.
        /// </summary>
        public long ReceivedBytes => Volatile.Read(ref _rxBytes);

        /// <summary>
        /// Gets the total number of short datagrams dropped (insufficient length).
        /// </summary>
        public long DroppedShort => Volatile.Read(ref _dropShort);

        /// <summary>
        /// Gets the total number of unauthorized datagrams dropped (failed auth, endpoint mismatch, replay check).
        /// </summary>
        public long DroppedUnauth => Volatile.Read(ref _dropUnauth);

        /// <summary>
        /// Gets the total number of unknown session datagrams dropped.
        /// </summary>
        public long DroppedUnknown => Volatile.Read(ref _dropUnknown);

        /// <summary>
        /// Gets the total number of rate-limited datagrams dropped.
        /// </summary>
        public long DroppedRateLimited => Volatile.Read(ref _dropRateLimited);

        /// <summary>
        /// Gets the total number of oversized datagrams dropped.
        /// </summary>
        public long DroppedOversize => Volatile.Read(ref _dropOversize);

        /// <summary>
        /// Gets the total number of socket receive errors.
        /// </summary>
        public long ReceiveErrors => Volatile.Read(ref _recvErrors);

        /// <summary>
        /// Gets the cumulative number of dropped datagrams across all drop categories.
        /// </summary>
        public long TotalDropped => this.DroppedShort + this.DroppedUnauth + this.DroppedUnknown + this.DroppedRateLimited + this.DroppedOversize;

        #endregion Properties

        #region Internal Methods

        /// <summary>
        /// Records a received packet.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_RX_PACKET() => Interlocked.Increment(ref _rxPackets);

        /// <summary>
        /// Records received bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_RX_BYTES(int bytes) => Interlocked.Add(ref _rxBytes, bytes);

        /// <summary>
        /// Records a short datagram drop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_DROP_SHORT() => Interlocked.Increment(ref _dropShort);

        /// <summary>
        /// Records an unauthorized datagram drop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_DROP_UNAUTH() => Interlocked.Increment(ref _dropUnauth);

        /// <summary>
        /// Records an unknown session datagram drop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_DROP_UNKNOWN() => Interlocked.Increment(ref _dropUnknown);

        /// <summary>
        /// Records a rate-limited datagram drop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_DROP_RATE_LIMITED() => Interlocked.Increment(ref _dropRateLimited);

        /// <summary>
        /// Records an oversized datagram drop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_DROP_OVERSIZE() => Interlocked.Increment(ref _dropOversize);

        /// <summary>
        /// Records a socket receive error.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_RECV_ERROR() => Interlocked.Increment(ref _recvErrors);

        #endregion Internal Methods
    }

    #endregion Nested Metrics Class

    /// <inheritdoc/>
    public UMetrics Metrics
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = new();
}
