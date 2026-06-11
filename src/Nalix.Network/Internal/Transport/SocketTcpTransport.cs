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

[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]

namespace Nalix.Network.Internal.Transport;

[SkipLocalsInit]
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SocketTcpTransport : IConnection.ISocketTransport
{
    #region Fields

    private readonly Connection _outer;
    private readonly SocketConnection _socket;
    private readonly SequenceCounter _sendSequence = new();
    private readonly SequenceCounter _receiveSequence = new();

    #endregion Fields

    #region Constructor

    public SocketTcpTransport(Connection outer, SocketConnection socket)
    {
        _outer = outer ?? throw new ArgumentNullException(nameof(outer));
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
    }

    #endregion Constructor

    #region Properties

    public TransportFraming Framing { get; private set; } = TransportFraming.None;

    public Socket Socket => _socket.Socket;

    public Task? ReceiveLoopTask => _socket.ReceiveLoopTask;

    public byte[]? StolenData => _socket.StolenData;

    public ISequenceCounter SendSequence => _sendSequence;

    public ISequenceCounter ReceiveSequence => _receiveSequence;

    #endregion Properties

    #region Methods

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Socket Unwrap() => _socket.Unwrap();

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_outer.IsDisposed, nameof(Connection));
        _socket.BeginReceive(cancellationToken);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UseFraming(TransportFraming framing)
    {
        this.Framing = framing;
        _socket.SetFraming(framing);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint NextSendSequence() => _sendSequence.Next();

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint NextReceiveSequence() => _receiveSequence.Next();

    public uint CurrentSendSequence => _sendSequence.Current();

    public uint CurrentReceiveSequence => _receiveSequence.Current();

    #endregion Methods
}
