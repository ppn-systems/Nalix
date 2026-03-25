// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Serializes all outbound frames through a bounded <see cref="System.Threading.Channels.Channel{T}"/>
/// so that concurrent callers never interleave bytes on the socket.
/// </summary>
/// <remarks>
/// <para>
/// The channel has a fixed capacity of <see cref="SendQueueCapacity"/> slots.
/// When the channel is full, <see cref="SendAsync(ReadOnlyMemory{byte}, CancellationToken)"/>
/// </para>
/// <para>
/// A single drain loop (<see cref="DRAIN_LOOP_ASYNC"/>) runs for the lifetime of the sender
/// and is the only task that actually writes to the socket — eliminating all send-side races.
/// </para>
/// <para>
/// <b>Ownership:</b> payloads enqueued as <c>byte[]</c> are rented from
/// <see cref="BufferPoolManager"/> and returned by the drain loop after the send completes.
/// </para>
/// </remarks>
internal sealed class FRAME_SENDER : IDisposable
{
    // ── Constants ────────────────────────────────────────────────────────────

    /// <summary>Maximum number of pending send items before callers start awaiting.</summary>
    public const int SendQueueCapacity = 1024;

    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly TransportOptions _options;
    private readonly Func<Socket> _getSocket;
    private readonly Action<int> _reportBytesSent;
    private readonly Action<Exception> _onError;

    /// <summary>
    /// Each item carries:
    /// <list type="bullet">
    ///   <item><description><c>frame</c>  — rented buffer containing the complete framed packet
    ///         (2-byte header + payload). Must be returned to <see cref="BufferLease.ByteArrayPool"/> by the drain loop.</description></item>
    ///   <item><description><c>frameLen</c> — number of valid bytes in <c>frame</c>.</description></item>
    ///   <item><description><c>tcs</c>    — completed with <c>true/false</c> when the drain loop
    ///         finishes or fails the send. The caller awaits this to get the send result.</description></item>
    /// </list>
    /// </summary>
    private readonly System.Threading.Channels.Channel<(
        byte[] frame,
        int frameLen,
        TaskCompletionSource<bool> tcs)> _sendQueue;

    /// <summary>
    /// CTS that stops the drain loop when the sender is disposed or the connection drops.
    /// </summary>
    private readonly CancellationTokenSource _drainCts = new();

    /// <summary>Dispose guard: 0 = live, 1 = disposed.</summary>
    private int _disposed;

    // ── Constructor ──────────────────────────────────────────────────────────

    internal FRAME_SENDER(
        Func<Socket> getSocket,
        TransportOptions options,
        Action<int> reportBytesSent,
        Action<Exception> onError)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
        _reportBytesSent = reportBytesSent ?? throw new ArgumentNullException(nameof(reportBytesSent));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));

        // BoundedChannelFullMode.Wait → callers await when queue is full (backpressure).
        _sendQueue = System.Threading.Channels.Channel.CreateBounded<(
            byte[],
            int,
            TaskCompletionSource<bool>)>(
            new System.Threading.Channels.BoundedChannelOptions(SendQueueCapacity)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                SingleReader = true,   // Only the drain loop reads.
                SingleWriter = false,  // Multiple callers may enqueue concurrently.
            });

        // Start the drain loop as a background task.
        _ = Task.Run(
            () => DRAIN_LOOP_ASYNC(_drainCts.Token),
            CancellationToken.None);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Enqueues <paramref name="payload"/> for sending and awaits until the drain loop
    /// has transmitted it (or failed).
    /// </summary>
    /// <param name="payload">Raw payload bytes (without framing header).</param>
    /// <param name="cancellationToken">Token to cancel the enqueue-wait or the send.</param>
    /// <returns>
    /// <c>true</c> if the frame was sent successfully; <c>false</c> on socket error.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="payload"/> exceeds <see cref="TransportOptions.MaxPacketSize"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled while waiting for a queue slot.
    /// </exception>
    public async Task<bool> SendAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) == 1, nameof(FRAME_SENDER));

        if (payload.Length > _options.MaxPacketSize)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload exceeds MaxPacketSize.");
        }

        // ── 1. Frame the packet into a rented buffer ──────────────────────
        //
        // We materialise the frame here (on the caller's thread) so the drain loop
        // only has to write bytes — no serialisation work on the hot path.

        int totalLen = TcpSession.HeaderSize + payload.Length;
        byte[] frame = BufferLease.ByteArrayPool.Rent(totalLen);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
            MemoryExtensions.AsSpan(frame, 0, TcpSession.HeaderSize),
            (ushort)totalLen);

        payload.Span.CopyTo(
            MemoryExtensions.AsSpan(frame, TcpSession.HeaderSize, payload.Length));

        // ── 2. Enqueue and await the result ───────────────────────────────
        //
        // TCS is RunContinuationsAsynchronously so the drain loop is never blocked
        // by continuations running inline on its thread.

        TaskCompletionSource<bool> tcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            // WriteAsync awaits when queue is full (BoundedChannelFullMode.Wait).
            await _sendQueue.Writer.WriteAsync(
                (frame, totalLen, tcs), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Enqueue failed (canceled or channel completed) — return the buffer now
            // because the drain loop will never see this item.
            try { BufferLease.ByteArrayPool.Return(frame); } catch { }
            throw;
        }

        // Await the drain loop's verdict.
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes <paramref name="packet"/> and enqueues it for sending.
    /// </summary>
    /// <param name="packet"></param>
    /// <param name="cancellationToken"></param>
    public Task<bool> SendAsync(
        IPacket packet,
        CancellationToken cancellationToken = default)
        => SendAsync(packet.Serialize(), cancellationToken);

    /// <summary>
    /// Stops the drain loop and completes all pending items with <c>false</c>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        // Stop the drain loop.
        try { _drainCts.Cancel(); } catch { }
        try { _drainCts.Dispose(); } catch { }

        // Signal no more items will be written, then drain remaining items as failed.
        _ = _sendQueue.Writer.TryComplete();

        while (_sendQueue.Reader.TryRead(out (byte[] frame, int frameLen, TaskCompletionSource<bool> tcs) item))
        {
            try { BufferLease.ByteArrayPool.Return(item.frame); } catch { }
            _ = item.tcs.TrySetResult(false);
        }
    }

    // ── Drain loop ───────────────────────────────────────────────────────────

    /// <summary>
    /// The single consumer that dequeues frames and writes them to the socket sequentially.
    /// Runs as a long-lived background task until <see cref="Dispose"/> is called or a fatal
    /// socket error occurs.
    /// </summary>
    /// <param name="token"></param>
    private async Task DRAIN_LOOP_ASYNC(
        CancellationToken token)
    {
        System.Threading.Channels.ChannelReader<(
            byte[] frame,
            int frameLen,
            TaskCompletionSource<bool> tcs)> reader = _sendQueue.Reader;

        try
        {
            // WaitToReadAsync suspends the loop efficiently when the queue is empty.
            while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (reader.TryRead(out (byte[] frame, int frameLen, TaskCompletionSource<bool> tcs) item))
                {
                    await SEND_FRAME_ASYNC(item.frame, item.frameLen, item.tcs, token)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown path — drain loop exits cleanly.
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[SDK.{nameof(FRAME_SENDER)}] drain-loop-faulted: {ex.Message}", ex);

            _onError(ex);
        }
        finally
        {
            // Fail any items that were already dequeued by TryRead but not yet sent,
            // plus anything still sitting in the channel.
            while (reader.TryRead(out (byte[] frame, int frameLen, TaskCompletionSource<bool> tcs) leftover))
            {
                try { BufferLease.ByteArrayPool.Return(leftover.frame); } catch { }
                _ = leftover.tcs.TrySetResult(false);
            }
        }
    }

    /// <summary>
    /// Writes a single pre-framed buffer to the socket, then notifies the caller via
    /// <paramref name="tcs"/> and returns the rented buffer to the pool.
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="frameLen"></param>
    /// <param name="tcs"></param>
    /// <param name="token"></param>
    /// <exception cref="SocketException"></exception>
    private async Task SEND_FRAME_ASYNC(
        byte[] frame,
        int frameLen,
        TaskCompletionSource<bool> tcs,
        CancellationToken token)
    {
        try
        {
            Socket s = _getSocket();

            int sent = 0;
            while (sent < frameLen)
            {
                int n = await s.SendAsync(
                    new ReadOnlyMemory<byte>(frame, sent, frameLen - sent),
                    SocketFlags.None,
                    token).ConfigureAwait(false);

                if (n == 0)
                {
                    throw new SocketException(
                        (int)SocketError.ConnectionReset);
                }

                sent += n;
            }

            try { _reportBytesSent(frameLen); } catch { }

            _ = tcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(FRAME_SENDER)}:{nameof(SEND_FRAME_ASYNC)}] send-error: {ex.Message}", ex);

            _ = tcs.TrySetResult(false);
            _onError(ex);

            // Re-throw so the drain loop can decide whether to continue or stop.
            throw;
        }
        finally
        {
            // Buffer ownership always returns here — regardless of success or failure.
            try { BufferLease.ByteArrayPool.Return(frame); } catch { }
        }
    }
}
