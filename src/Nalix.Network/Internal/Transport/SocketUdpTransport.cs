// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;

namespace Nalix.Network.Internal.Transport;

[DebuggerNonUserCode]
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SocketUdpTransport : IConnection.IUdp, IPoolable, IDisposable
{
    #region Constants

    private const int UdpReplaySoftLimit = 4_096;
    private const long UdpReplayCleanupIntervalMs = 5_000;

    #endregion Constants

    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private EndPoint? _endPoint;
    private Connection? _outer;
    private Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    private long _udpReplayLastCleanupMs;
    private readonly ConcurrentDictionary<ulong, long> _udpReplayNonces = new();

    #endregion Fields

    internal bool TryAcceptUdpNonce(ulong nonce, long timestamp, long maxReplayWindowMs)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long expiryCutoff = now - maxReplayWindowMs;

        if (!_udpReplayNonces.TryAdd(nonce, timestamp))
        {
            return false;
        }

        if (_udpReplayNonces.Count >= UdpReplaySoftLimit ||
            now - Interlocked.Read(ref _udpReplayLastCleanupMs) >= UdpReplayCleanupIntervalMs)
        {
            CLEANUP_UDP_REPLAY_NONCES(expiryCutoff, now);
        }

        return true;

        void CLEANUP_UDP_REPLAY_NONCES(long expiryCutoff, long now)
        {
            if (Interlocked.Exchange(ref _udpReplayLastCleanupMs, now) > now - UdpReplayCleanupIntervalMs)
            {
                return;
            }

            foreach (KeyValuePair<ulong, long> entry in _udpReplayNonces)
            {
                if (entry.Value < expiryCutoff)
                {
                    _ = _udpReplayNonces.TryRemove(entry.Key, out _);
                }
            }
        }
    }

    internal void Attach(Connection outer) => _outer = outer;

    public void Initialize(ref IPEndPoint iPEndPoint)
    {
        _endPoint = iPEndPoint;
        AddressFamily af = iPEndPoint.AddressFamily;
        if (_socket.AddressFamily != af)
        {
            _socket.Dispose();
            _socket = new Socket(af, SocketType.Dgram, ProtocolType.Udp);
        }
        if (af == AddressFamily.InterNetworkV6)
        {
            try { _socket.DualMode = true; } catch { }
        }
        const int BufferSize = (int)(1024 * 1.35);
        _socket.SendBufferSize = BufferSize;
        _socket.ReceiveBufferSize = BufferSize;
        _socket.Bind(_endPoint);
    }

    public void Send(IPacket packet)
    {
        if (packet.Length == 0)
        {
            throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
        }

        if (packet.Length < BufferLease.StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[packet.Length * 110 / 100];
            int bytesWritten = packet.Serialize(buffer);
            this.Send(buffer[..bytesWritten]);
            return;
        }
        using BufferLease lease = BufferLease.Rent(packet.Length);
        int bytesWrittenHeap = packet.Serialize(lease.SpanFull);
        lease.CommitLength(bytesWrittenHeap);
        this.Send(lease.Span);
    }

    public void Send(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty || _endPoint is null)
        {
            throw new NetworkException("Connection endpoint is not available.");
        }

        int sent = _socket.SendTo(message, SocketFlags.None, _endPoint);
        if (sent != message.Length)
        {
            throw new NetworkException("The socket did not send the full payload.");
        }
    }

    public async Task SendAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        if (packet.Length == 0)
        {
            throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
        }

        if (packet.Length < BufferLease.StackAllocThreshold)
        {
            byte[] buffer = new byte[packet.Length * 110 / 100];
            int bytesWritten = packet.Serialize(buffer);
            await this.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesWritten), cancellationToken).ConfigureAwait(false);
            return;
        }
        using BufferLease lease = BufferLease.Rent(packet.Length);
        int bytesWrittenHeap = packet.Serialize(lease.SpanFull);
        lease.CommitLength(bytesWrittenHeap);
        await this.SendAsync(lease.Memory, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.IsEmpty)
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        if (_endPoint is null)
        {
            throw new NetworkException("Connection endpoint is not available.");
        }

        int sentBytes = await _socket.SendToAsync(message, _endPoint, cancellationToken).ConfigureAwait(false);
        if (sentBytes != message.Length)
        {
            throw new NetworkException("The socket did not send the full payload.");
        }
    }

    public void ResetForPool()
    {
        _outer = null;
        _endPoint = null;
        _socket.Dispose();
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    public void Dispose() { }
}
