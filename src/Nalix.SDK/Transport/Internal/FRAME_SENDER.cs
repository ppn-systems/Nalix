// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
/// When the channel is full, <see cref="SendAsync(System.ReadOnlyMemory{System.Byte}, System.Threading.CancellationToken)"/>
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
internal sealed class FRAME_SENDER : System.IDisposable
{
    // ── Constants ────────────────────────────────────────────────────────────

    /// <summary>Maximum number of pending send items before callers start awaiting.</summary>
    public const System.Int32 SendQueueCapacity = 1024;

    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly TransportOptions _options;
    private readonly System.Func<System.Net.Sockets.Socket> _getSocket;
    private readonly System.Action<System.Int32> _reportBytesSent;
    private readonly System.Action<System.Exception> _onError;

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
        System.Byte[] frame,
        System.Int32 frameLen,
        System.Threading.Tasks.TaskCompletionSource<System.Boolean> tcs)> _sendQueue;

    /// <summary>
    /// CTS that stops the drain loop when the sender is disposed or the connection drops.
    /// </summary>
    private readonly System.Threading.CancellationTokenSource _drainCts = new();

    /// <summary>Dispose guard: 0 = live, 1 = disposed.</summary>
    private System.Int32 _disposed;

    // ── Constructor ──────────────────────────────────────────────────────────

    internal FRAME_SENDER(
        System.Func<System.Net.Sockets.Socket> getSocket,
        TransportOptions options,
        System.Action<System.Int32> reportBytesSent,
        System.Action<System.Exception> onError)
    {
        _options = options ?? throw new System.ArgumentNullException(nameof(options));
        _getSocket = getSocket ?? throw new System.ArgumentNullException(nameof(getSocket));
        _reportBytesSent = reportBytesSent ?? throw new System.ArgumentNullException(nameof(reportBytesSent));
        _onError = onError ?? throw new System.ArgumentNullException(nameof(onError));

        // BoundedChannelFullMode.Wait → callers await when queue is full (backpressure).
        _sendQueue = System.Threading.Channels.Channel.CreateBounded<(
            System.Byte[],
            System.Int32,
            System.Threading.Tasks.TaskCompletionSource<System.Boolean>)>(
            new System.Threading.Channels.BoundedChannelOptions(SendQueueCapacity)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                SingleReader = true,   // Only the drain loop reads.
                SingleWriter = false,  // Multiple callers may enqueue concurrently.
            });

        // Start the drain loop as a background task.
        _ = System.Threading.Tasks.Task.Run(
            () => DRAIN_LOOP_ASYNC(_drainCts.Token),
            System.Threading.CancellationToken.None);
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
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="payload"/> exceeds <see cref="TransportOptions.MaxPacketSize"/>.
    /// </exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled while waiting for a queue slot.
    /// </exception>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.ReadOnlyMemory<System.Byte> payload,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(
            System.Threading.Volatile.Read(ref _disposed) == 1, nameof(FRAME_SENDER));

        if (payload.Length > _options.MaxPacketSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(payload), "Payload exceeds MaxPacketSize.");
        }

        // ── 1. Frame the packet into a rented buffer ──────────────────────
        //
        // We materialise the frame here (on the caller's thread) so the drain loop
        // only has to write bytes — no serialisation work on the hot path.

        System.Int32 totalLen = TcpSession.HeaderSize + payload.Length;
        System.Byte[] frame = BufferLease.ByteArrayPool.Rent(totalLen);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
            System.MemoryExtensions.AsSpan(frame, 0, TcpSession.HeaderSize),
            (System.UInt16)totalLen);

        payload.Span.CopyTo(
            System.MemoryExtensions.AsSpan(frame, TcpSession.HeaderSize, payload.Length));

        // ── 2. Enqueue and await the result ───────────────────────────────
        //
        // TCS is RunContinuationsAsynchronously so the drain loop is never blocked
        // by continuations running inline on its thread.

        System.Threading.Tasks.TaskCompletionSource<System.Boolean> tcs = new(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

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
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(
        IPacket packet,
        System.Threading.CancellationToken cancellationToken = default)
        => SendAsync(packet.Serialize(), cancellationToken);

    /// <summary>
    /// Stops the drain loop and completes all pending items with <c>false</c>.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        // Stop the drain loop.
        try { _drainCts.Cancel(); } catch { }
        try { _drainCts.Dispose(); } catch { }

        // Signal no more items will be written, then drain remaining items as failed.
        _sendQueue.Writer.TryComplete();

        while (_sendQueue.Reader.TryRead(out var item))
        {
            try { BufferLease.ByteArrayPool.Return(item.frame); } catch { }
            item.tcs.TrySetResult(false);
        }
    }

    // ── Drain loop ───────────────────────────────────────────────────────────

    /// <summary>
    /// The single consumer that dequeues frames and writes them to the socket sequentially.
    /// Runs as a long-lived background task until <see cref="Dispose"/> is called or a fatal
    /// socket error occurs.
    /// </summary>
    private async System.Threading.Tasks.Task DRAIN_LOOP_ASYNC(
        System.Threading.CancellationToken token)
    {
        System.Threading.Channels.ChannelReader<(
            System.Byte[] frame,
            System.Int32 frameLen,
            System.Threading.Tasks.TaskCompletionSource<System.Boolean> tcs)> reader = _sendQueue.Reader;

        try
        {
            // WaitToReadAsync suspends the loop efficiently when the queue is empty.
            while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    await SEND_FRAME_ASYNC(item.frame, item.frameLen, item.tcs, token)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown path — drain loop exits cleanly.
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[SDK.{nameof(FRAME_SENDER)}] drain-loop-faulted: {ex.Message}", ex);

            _onError(ex);
        }
        finally
        {
            // Fail any items that were already dequeued by TryRead but not yet sent,
            // plus anything still sitting in the channel.
            while (reader.TryRead(out var leftover))
            {
                try { BufferLease.ByteArrayPool.Return(leftover.frame); } catch { }
                leftover.tcs.TrySetResult(false);
            }
        }
    }

    /// <summary>
    /// Writes a single pre-framed buffer to the socket, then notifies the caller via
    /// <paramref name="tcs"/> and returns the rented buffer to the pool.
    /// </summary>
    private async System.Threading.Tasks.Task SEND_FRAME_ASYNC(
        System.Byte[] frame,
        System.Int32 frameLen,
        System.Threading.Tasks.TaskCompletionSource<System.Boolean> tcs,
        System.Threading.CancellationToken token)
    {
        try
        {
            System.Net.Sockets.Socket s = _getSocket();

            System.Int32 sent = 0;
            while (sent < frameLen)
            {
                System.Int32 n = await s.SendAsync(
                    new System.ReadOnlyMemory<System.Byte>(frame, sent, frameLen - sent),
                    System.Net.Sockets.SocketFlags.None,
                    token).ConfigureAwait(false);

                if (n == 0)
                {
                    throw new System.Net.Sockets.SocketException(
                        (System.Int32)System.Net.Sockets.SocketError.ConnectionReset);
                }

                sent += n;
            }

            try { _reportBytesSent(frameLen); } catch { }

            tcs.TrySetResult(true);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.{nameof(FRAME_SENDER)}:{nameof(SEND_FRAME_ASYNC)}] send-error: {ex.Message}", ex);

            tcs.TrySetResult(false);
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