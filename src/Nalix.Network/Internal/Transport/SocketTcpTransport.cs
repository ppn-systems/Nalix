// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Security;
using Nalix.Environment.Sequencing;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Abstractions;

[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]

namespace Nalix.Network.Internal.Transport;

[SkipLocalsInit]
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SocketTcpTransport : IConnection.ISocketTransport, IDisposable
{
    #region Fields

    private readonly Connection _outer;
    private readonly SocketConnection _socket;
    private readonly ISequenceCounter _sendSequence;
    private readonly ISequenceCounter _receiveSequence;

    #endregion Fields

    #region Properties

    public TransportFraming Framing { get; private set; }

    /// <inheritdoc/>
    public System.Net.Sockets.Socket Socket => _socket.Socket;

    /// <inheritdoc/>
    public ISequenceCounter SendSequence => _sendSequence;

    /// <inheritdoc/>
    public ISequenceCounter ReceiveSequence => _receiveSequence;

    /// <inheritdoc/>
    public long BytesSent => _socket.BytesSent;

    /// <inheritdoc/>
    public long BytesReceived => _socket.BytesReceived;

    /// <inheritdoc/>
    public long LastPingTime
    {
        get => _socket.LastPingTime;
        set => _socket.LastPingTime = value;
    }

    #endregion Properties

    #region Constructor

    /// <inheritdoc/>
    public SocketTcpTransport(Socket socket, Connection connection, ITransportEventSink eventSink)
    {
        _sendSequence = new SequenceCounter();
        _receiveSequence = new SequenceCounter();
        _outer = connection;
        _socket = new SocketConnection(socket, connection, eventSink);
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
    public Task? ReceiveLoopTask => _socket.ReceiveLoopTask;

    /// <inheritdoc/>
    public byte[]? StolenData => _socket.StolenData;

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Send(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty)
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        SocketConnection.SendResult result = _socket.Send(message);
        if (result != SocketConnection.SendResult.Success)
        {
            throw Throw.GetSendFailed();
        }
    }

    /// <inheritdoc/>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.IsEmpty)
        {
            return ValueTask.FromException(new ArgumentException("Message must not be empty.", nameof(message)));
        }

        ValueTask<SocketConnection.SendResult> vt = _socket.SendAsync(message, cancellationToken);
        if (vt.IsCompletedSuccessfully)
        {
            SocketConnection.SendResult result = vt.Result;
            if (result != SocketConnection.SendResult.Success)
            {
                return ValueTask.FromException(Throw.GetSendFailed());
            }
            return default;
        }

        return AWAIT_SEND_ASYNC(vt);

        static async ValueTask AWAIT_SEND_ASYNC(ValueTask<SocketConnection.SendResult> vt)
        {
            SocketConnection.SendResult result = await vt.ConfigureAwait(false);
            if (result != SocketConnection.SendResult.Success)
            {
                throw Throw.GetSendFailed();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _socket.Dispose();

    #endregion APIs
}
