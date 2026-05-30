// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Security;
using Nalix.Environment.Configuration;
using Nalix.Environment.Memory;
using Nalix.Environment.Options;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Security;
using Nalix.Network.Options;

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Adapter class that implements <see cref="IConnection.ITransport"/> for WebSocket.
/// </summary>
[SkipLocalsInit]
[DebuggerNonUserCode]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class WebSocketTransport : IConnection.ITransport, IDisposable
{
    #region Fields

    private readonly WebSocketConnection _owner;
    private readonly NetworkWebSocketOptions _options;

    private int _disposed;
    private int _receiveStarted;
    private Task? _receiveLoopTask;
    private TransportSequencer _sequencer;

    #endregion Fields

    #region Constructor

    public WebSocketTransport(WebSocketConnection owner)
    {
        _sequencer = new TransportSequencer();
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _options = ConfigurationManager.Instance.Get<NetworkWebSocketOptions>();
    }

    #endregion Constructor

    #region Properties

    /// <inheritdoc/>
    public TransportFraming Framing => TransportFraming.None;

    /// <inheritdoc/>
    public ISequenceCounter SendSequence => _sequencer.SendSequence;

    /// <inheritdoc/>
    public ISequenceCounter ReceiveSequence => _sequencer.ReceiveSequence;

    #endregion Properties

    #region APIs

    [StackTraceHidden]
    public void Send(ReadOnlySpan<byte> message) => Throw.WebSocketSyncSendNotSupported();

    [StackTraceHidden]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (_owner.IsDisposed || _owner.WebSocket.State != WebSocketState.Open)
        {
            Throw.WebSocketClosed();
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
    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, nameof(WebSocketConnection));

        if (Interlocked.CompareExchange(ref _receiveStarted, 1, 0) != 0)
        {
            return;
        }

        _receiveLoopTask = this.RECEIVE_LOOP_ASYNC(cancellationToken);
    }

    /// <inheritdoc/>
    public void UseFraming(TransportFraming framing)
    {
        if (framing != TransportFraming.None)
        {
            throw new NotSupportedException("WebSocket transport does not support framing mechanisms natively as it provides its own message framing. Use TransportFraming.None.");
        }
    }

    #endregion APIs

    #region Receive Loop

    private static readonly FragmentOptions s_fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();

    private static int GET_RECEIVE_BUFFER_SIZE()
    {
        if (s_fragmentOptions.MaxChunkSize <= 0)
        {
            Throw.WebSocketInvalidReceiveBuffer();
        }

        return s_fragmentOptions.MaxChunkSize;
    }

    private async Task RECEIVE_LOOP_ASYNC(CancellationToken cancellationToken = default)
    {
        // Allocate a buffer matching FragmentOptions.MaxChunkSize (typically 1.4KB) to eliminate 64KB per-connection bloat.
        int length = GET_RECEIVE_BUFFER_SIZE();
        byte[] buffer = BufferLease.ByteArrayPool.Rent(length);

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
                    if (result.Count > _options.MaxMessageSize)
                    {
                        Throw.WebSocketMessageTooLarge();
                    }

                    if (result.Count == 0)
                    {
                        return;
                    }

                    // Rent a lease and copy data so the receive loop can continue immediately
                    BufferLease lease = BufferLease.CopyFrom(new ReadOnlySpan<byte>(buffer, 0, result.Count));
                    lease.IsReliable = true;

                    _owner.TriggerProcessEvent(lease);
                }
                else
                {
                    // Slow path: the message is larger than the buffer, we need to assemble it
                    await this.HANDLE_LARGE_MESSAGE_ASYNC(buffer, result.Count, cancellationToken).ConfigureAwait(false);
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

    private async Task HANDLE_LARGE_MESSAGE_ASYNC(byte[] initialBuffer, int initialBytes, CancellationToken cancellationToken)
    {
        if (initialBytes > _options.MaxMessageSize)
        {
            Throw.WebSocketMessageTooLarge();
        }

        byte[] payload = BufferLease.ByteArrayPool.Rent(_options.MaxMessageSize);
        int written = initialBytes;
        initialBuffer.AsSpan(0, initialBytes).CopyTo(payload);

        int length = GET_RECEIVE_BUFFER_SIZE();
        byte[] buffer = BufferLease.ByteArrayPool.Rent(length);
        bool ownershipTransferred = false;
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

                int nextWritten = checked(written + result.Count);

                if (nextWritten > _options.MaxMessageSize)
                {
                    Throw.WebSocketMessageTooLarge();
                }

                buffer.AsSpan(0, result.Count).CopyTo(payload.AsSpan(written));
                written = nextWritten;
                _owner.AddBytesReceived(result.Count);

            } while (!result.EndOfMessage);

            if (written == 0)
            {
                BufferLease.ByteArrayPool.Return(buffer);
                return;
            }

            BufferLease lease = BufferLease.TakeOwnership(payload, 0, written);
            lease.IsReliable = true;

            _owner.TriggerProcessEvent(lease);

            ownershipTransferred = true;
        }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);

            if (!ownershipTransferred)
            {
                BufferLease.ByteArrayPool.Return(payload);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OBSERVE_RECEIVE_LOOP_SHUTDOWN(Task receiveLoopTask)
    {
        if (receiveLoopTask.IsCompleted)
        {
            if (receiveLoopTask.Exception?.GetBaseException() is Exception ex)
            {
                LOG_RECEIVE_LOOP_FAULT(_owner, ex, "during-dispose");
            }
            return;
        }

        _ = receiveLoopTask.ContinueWith(static (task, state) =>
        {
            if (state is not WebSocketConnection owner)
            {
                return;
            }

            if (task.Exception?.GetBaseException() is Exception ex)
            {
                LOG_RECEIVE_LOOP_FAULT(owner, ex, "after-dispose");
            }
        }, _owner, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static void LOG_RECEIVE_LOOP_FAULT(WebSocketConnection owner, Exception ex, string phase)
    {
        if (owner.Logger != null && owner.Logger.IsEnabled(LogLevel.Debug))
        {
            owner.Logger.LogDebug(ex, $"[NW.{nameof(WebSocketTransport)}:{nameof(Dispose)}] receive-loop-faulted-{phase}");
        }
    }

    #endregion Receive Loop

    #region Dispose

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        Task? receiveLoopTask = Interlocked.Exchange(ref _receiveLoopTask, null);
        if (receiveLoopTask is not null)
        {
            this.OBSERVE_RECEIVE_LOOP_SHUTDOWN(receiveLoopTask);
        }
    }

    #endregion Dispose
}
