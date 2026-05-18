// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Security;
using Nalix.Environment.Configuration;
using Nalix.Environment.Memory;
using Nalix.Environment.Options;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Security;

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Adapter class that implements <see cref="IConnection.ITransport"/> for WebSocket.
/// </summary>
[SkipLocalsInit]
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class WebSocketTransport : IConnection.ITransport
{
    #region Fields

    private readonly WebSocketConnection _owner;
    private TransportSequencer _sequencer;

    #endregion Fields

    #region Constructor

    public WebSocketTransport(WebSocketConnection owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _sequencer = new TransportSequencer();
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc/>
    public ISequenceCounter SendSequence => _sequencer.SendSequence;

    /// <inheritdoc/>
    public ISequenceCounter ReceiveSequence => _sequencer.ReceiveSequence;

    #endregion Properties

    #region APIs

    [StackTraceHidden]
    public void Send(IPacket packet) => this.SendAsync(packet).AsTask().GetAwaiter().GetResult();

    [StackTraceHidden]
    public void Send(ReadOnlySpan<byte> message) => this.SendAsync(message.ToArray()).AsTask().GetAwaiter().GetResult();

    [StackTraceHidden]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask SendAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        byte[] bytes = packet.Serialize();
        await this.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    [StackTraceHidden]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (_owner.IsDisposed || _owner.WebSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is closed.");
        }

        // WebSockets handle framing natively, so we just send the message as binary.
        // A SemaphoreSlim is used because WebSocket.SendAsync doesn't support concurrent calls.
        await _owner.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _owner.WebSocket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _owner.SendLock.Release();
        }

        _owner.AddBytesSent(message.Length);
        _owner.TriggerPostProcessEvent();
    }

    [StackTraceHidden]
    public void BeginReceive(CancellationToken cancellationToken = default) => _ = this.StartReceiveLoopAsync(cancellationToken);

    #endregion APIs

    #region Receive Loop

    private static readonly FragmentOptions s_fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();

    private static int GET_RECEIVE_BUFFER_SIZE()
    {
        if (s_fragmentOptions.MaxChunkSize <= 0)
        {
            throw new InvalidOperationException(
                $"[{nameof(WebSocketTransport)}] Invalid configuration: " +
                $"MaxChunkSize must be > 0, got {s_fragmentOptions.MaxChunkSize}.");
        }

        return s_fragmentOptions.MaxChunkSize;
    }

    private async Task StartReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        // Allocate a buffer matching FragmentOptions.MaxChunkSize (typically 1.4KB) to eliminate 64KB per-connection bloat.
        int length = GET_RECEIVE_BUFFER_SIZE();
        byte[] buffer = BufferLease.ByteArrayPool.Rent(GET_RECEIVE_BUFFER_SIZE());

        try
        {
            while (!cancellationToken.IsCancellationRequested && _owner.WebSocket.State == WebSocketState.Open && !_owner.IsDisposed)
            {
                WebSocketReceiveResult result = await _owner.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, length), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                _owner.AddBytesReceived(result.Count);
                _owner.UpdateLastPingTime();

                if (result.EndOfMessage)
                {
                    // Fast path: the entire message fit in our buffer
                    this.DispatchPayload(buffer, 0, result.Count);
                }
                else
                {
                    // Slow path: the message is larger than the buffer, we need to assemble it
                    await this.HandleLargeMessageAsync(buffer, result.Count, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException)
        {
            // Disconnected
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_owner.Logger != null && _owner.Logger.IsEnabled(LogLevel.Error))
            {
                _owner.Logger.LogError(ex, $"[NW.{nameof(WebSocketConnection)}] Receive loop error");
            }
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);
            _owner.Disconnect("Receive loop exited");
        }
    }

    private async Task HandleLargeMessageAsync(byte[] initialBuffer, int initialBytes, CancellationToken cancellationToken)
    {
        using MemoryStream ms = new();
        await ms.WriteAsync(initialBuffer.AsMemory(0, initialBytes), cancellationToken).ConfigureAwait(false);

        int length = GET_RECEIVE_BUFFER_SIZE();
        byte[] buffer = BufferLease.ByteArrayPool.Rent(length);
        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await _owner.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, length), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                await ms.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
                _owner.AddBytesReceived(result.Count);

            } while (!result.EndOfMessage);

            // Dispatch the fully assembled message
            if (ms.TryGetBuffer(out ArraySegment<byte> segment))
            {
                this.DispatchPayload(segment.Array!, segment.Offset, segment.Count);
            }
            else
            {
                byte[] array = ms.ToArray();
                this.DispatchPayload(array, 0, array.Length);
            }
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);
        }
    }

    private void DispatchPayload(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return;
        }

        // Rent a lease and copy data so the receive loop can continue immediately
        BufferLease lease = BufferLease.CopyFrom(new ReadOnlySpan<byte>(buffer, offset, count));
        lease.IsReliable = true;

        _owner.TriggerProcessEvent(lease);
    }

    #endregion Receive Loop
}
