// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Security;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Abstractions;
using Nalix.Network.Internal.Security;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

[SkipLocalsInit]
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SocketTcpTransport : IConnection.ITransport, IDisposable
{
    #region Fields

    private readonly Connection _outer;
    private readonly SocketConnection _socket;
    private readonly TransportSequencer _sequencer;

    #endregion Fields

    #region Properties

    public TransportFraming Framing { get; private set; }

    /// <inheritdoc/>
    public ISequenceCounter SendSequence => _sequencer.SendSequence;

    /// <inheritdoc/>
    public ISequenceCounter ReceiveSequence => _sequencer.ReceiveSequence;

    /// <inheritdoc/>
    public long BytesSent => _socket.BytesSent;

    /// <inheritdoc/>
    public long BytesReceived => _socket.BytesReceived;

    /// <inheritdoc/>
    public long Uptime => _socket.Uptime;

    /// <inheritdoc/>
    public long LastPingTime
    {
        get => _socket.LastPingTime;
        set => _socket.LastPingTime = value;
    }

    #endregion Properties

    #region Constructor

    /// <inheritdoc/>
    public SocketTcpTransport(Socket socket, Connection connection, ITransportEventSink eventSink, ILogger? logger)
    {
        _sequencer = new();
        _outer = connection;
        _socket = new SocketConnection(socket, connection, eventSink, logger);
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_outer.IsDisposed, nameof(Connection));
        _socket.BeginReceive(cancellationToken);
    }

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UseFraming(TransportFraming framing)
    {
        this.Framing = framing;
        _socket.SetFraming(framing);
    }

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InjectPreReadBytes(ReadOnlySpan<byte> preReadData) => _socket.InjectPreReadBytes(preReadData);

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Net.Sockets.Socket Unwrap() => _socket.Unwrap();

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Send(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty)
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        _socket.Send(message);
    }

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.IsEmpty)
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        await _socket.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _socket.Dispose();

    #endregion APIs
}
