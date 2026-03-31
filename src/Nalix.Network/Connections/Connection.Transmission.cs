// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Time;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Connections;

public sealed partial class Connection
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IConnection.IUdp GetOrCreateUDP(ref IPEndPoint iPEndPoint)
    {
        ArgumentNullException.ThrowIfNull(iPEndPoint);

        if (this.UdpTransport == null)
        {
            this.UdpTransport = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                           .Get<SocketUdpTransport>();
            this.UdpTransport.Attach(this);
            this.UdpTransport.Initialize(ref iPEndPoint);
        }

        return this.UdpTransport;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void InjectIncoming(IBufferLease lease)
    {
        this.Socket.LastPingTime = (long)Clock.UnixTime().TotalMilliseconds;
        lease.Retain();

        ConnectionEventArgs args = s_pool.Get<ConnectionEventArgs>();
        args.Initialize(lease, this);

        _ = Internal.Transport.AsyncCallback.Invoke(OnProcessEventBridge, this, args);

#if DEBUG
        s_logger.Debug($"[NW.{nameof(SocketConnection)}:{this.InjectIncoming}] inject-bytes len={lease.Length}");
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal void ReleasePendingPacket() => this.Socket.OnPacketProcessed();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddBytesSent(int count) => _ = Interlocked.Add(ref _bytesSent, count);
}
