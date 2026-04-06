// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Results.Primitives;

namespace Nalix.Network.Internal.Transport;

[DebuggerNonUserCode]
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SocketTcpTransport(Connection outer) : IConnection.ITcp
{
    private readonly Connection _outer = outer;

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_outer.IsDisposed, nameof(Connection));
        _outer.Socket.BeginReceive(cancellationToken);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Send(IPacket packet)
    {
        if (packet.Length == 0)
        {
            throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
        }

        if (packet.Length < BufferLease.StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[packet.Length * 4];
            int bytesWritten = packet.Serialize(buffer);
            _outer.AddBytesSent(bytesWritten);
            this.Send(buffer[..bytesWritten]);
            return;
        }

        using BufferLease lease = BufferLease.Rent(packet.Length * 4);
        int bytesWrittenHeap = packet.Serialize(lease.SpanFull);
        lease.CommitLength(bytesWrittenHeap);
        _outer.AddBytesSent(bytesWrittenHeap);
        this.Send(lease.Span);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Send(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty)
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        _outer.Socket.Send(message);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task SendAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        if (packet.Length == 0)
        {
            throw new ArgumentException("Packet length must be greater than zero.", nameof(packet));
        }

        if (packet.Length < BufferLease.StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[packet.Length * 4];
            int bytesWritten = packet.Serialize(buffer);
            _outer.AddBytesSent(bytesWritten);
            await this.SendAsync(new ReadOnlyMemory<byte>(buffer[..bytesWritten].ToArray()), cancellationToken).ConfigureAwait(false);
            return;
        }

        using BufferLease lease = BufferLease.Rent(packet.Length * 4);
        int bytesWrittenHeap = packet.Serialize(lease.SpanFull);
        lease.CommitLength(bytesWrittenHeap);
        _outer.AddBytesSent(bytesWrittenHeap);
        await this.SendAsync(lease.Memory, cancellationToken).ConfigureAwait(false);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.IsEmpty)
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        await _outer.Socket.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

}
