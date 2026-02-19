// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    #region Fields

    // Thread BelowNormal is separate so that drain channel — is separate from ThreadPool.
    // WHY separate thread: This thread runs continuously; using ThreadPool will occupy the thread pool
    // and compete with more important async callback I/O.
    private Thread? _processThread;

    // Bounded channel: N producers (accept-loop workers) -> 1 consumer (BelowNormal thread).
    //
    // Producer-Consumer Architecture:
    // - N producers: accept-workers run in parallel, each worker accepts 1 conn and then DISPATCH.
    // - 1 consumer: _processThread drain channel and call ProcessConnection.
    //
    // WHY Channel instead of ConcurrentQueue + ManualResetEvent:
    // - Channel has WaitToReadAsync() async-aware -> consumer thread not busy-wait.
    // - BoundedChannel has a built-in backpressure (DropWrite when full).
    // - A more concise API, more deferred in terms of concurrency.
    private System.Threading.Channels.Channel<IConnection>? _processChannel;

    #endregion Fields

    #region Lifecycle

    /// <summary>
    /// Creates the channel and starts the consumer thread.
    /// Call once during listener startup inside <c>Activate</c>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private void START_PROCESS_CHANNEL(CancellationToken cancellationToken)
    {
        _processChannel = System.Threading.Channels.Channel.CreateBounded<IConnection>(
            new System.Threading.Channels.BoundedChannelOptions(s_config.ProcessChannelCapacity)
            {
                SingleReader = true,   // only the consumer thread reads
                SingleWriter = false,  // many accept-loop workers write

                // DropWrite = TryWrite returns false when the channel is full (no block producer).
                // WHY DropWrite instead of Wait: If block producer -> accept-worker is held
                // -> Cannot accept new connection -> latency increases and may fail.
                // DropWrite + log -> DDoS protection: new connection dropped, old connection secure.
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropWrite,

                // AllowSynchronousContinuations = false -> continuation is always scheduled
                // Go to ThreadPool instead of running inline on the writer's thread.
                // WHY: Avoid stack overflow and unexpected re-entrancy on producer thread.
                AllowSynchronousContinuations = false,
            });

        _processThread = new Thread(() => this.PROCESS_CHANNEL_LOOP(cancellationToken))
        {
            // Does not prevent the process from exiting when the main thread finishes.
            IsBackground = true,
            Name = $"{TaskNaming.Tags.Tcp}.{TaskNaming.Tags.Accept}.{TaskNaming.Tags.Dispatch}.{_port}",

            // BelowNormal priority -> OS scheduler prioritizes Normal-priority ThreadPool thread.
            // WHY: AsyncCallback of the I/O socket running on ThreadPool (Normal priority).
            // If consumer thread is on the same priority -> CPU competition -> I/O callback is delayed.
            // BelowNormal -> I/O callback always wins against CPU -> better throughput under high load.
            Priority = ThreadPriority.BelowNormal,
        };

        _processThread.Start();
    }

    /// <summary>
    /// Completes the channel and waits for the consumer thread to exit.
    /// Call during listener shutdown inside <c>Deactivate</c>.
    /// </summary>
    private void STOP_PROCESS_CHANNEL()
    {
        // TryComplete() -> đánh dấu channel là "đã đóng" (sẽ không nhận thêm item mới).
        // Consumer thread sẽ drain hết item còn lại rồi thoát khỏi vòng lặp.
        _ = (_processChannel?.Writer.TryComplete());

        // Join với timeout 5 giây -> tránh block vô hạn nếu consumer thread bị treo.
        // WHY 5s: Đủ để consumer drain hết item còn lại trong queue khi shutdown.
        _ = (_processThread?.Join(millisecondsTimeout: 5_000));
    }

    #endregion Lifecycle

    #region Producer — called from AcceptConnectionsAsync

    /// <summary>
    /// Writes a newly accepted connection into the channel.
    /// Replaces <c>ThreadPool.UnsafeQueueUserWorkItem</c> in
    /// <c>AcceptConnectionsAsync</c> — call like:
    /// <c>DISPATCH_CONNECTION(connection);</c>
    /// </summary>
    /// <param name="connection"></param>
    private void DISPATCH_CONNECTION(IConnection connection)
    {
        System.Threading.Channels.Channel<IConnection>? processChannel = _processChannel;
        if (processChannel is null)
        {
            s_logger?.Warn("[NW.{Class}:{Action}] process-channel-unavailable remote={RemoteEndPoint} port={Port}",
                nameof(TcpListenerBase), nameof(DISPATCH_CONNECTION), connection?.NetworkEndpoint.ToString() ?? "<null>", _port);

            ArgumentNullException.ThrowIfNull(connection);
            connection.Close();
            return;
        }

        if (processChannel.Writer.TryWrite(connection))
        {
            s_logger?.Trace("[NW.{Class}:{Action}] queued remote={RemoteEndPoint} port={Port}",
                nameof(TcpListenerBase), nameof(DISPATCH_CONNECTION), connection?.NetworkEndpoint.ToString() ?? "<null>", _port);

            return;
        }

        // Channel full (FullMode = DropWrite) -> drop new connection.
        // WHY drop new instead of drop old:
        // - The old connection in the channel has been accepted and may be legitimate user.
        // - The new connection (dropped) could be part of a DDoS burst -> drop is correct.
        // - Backpressure signal: If the channel is consistently full, increase ProcessChannelCapacity or
        //   optimize ProcessConnection for faster execution.
        this.Metrics.RECORD_REJECTED();

        s_logger?.Warn("[NW.{Class}:{Action}] channel-full remote={RemoteEndPoint} port={Port} — dropped",
            nameof(TcpListenerBase), nameof(DISPATCH_CONNECTION), connection?.NetworkEndpoint.ToString() ?? "<null>", _port);

        ArgumentNullException.ThrowIfNull(connection);
        connection.Close();
    }

    #endregion Producer — called from AcceptConnectionsAsync

    #region Consumer — BelowNormal background thread

    private void PROCESS_CHANNEL_LOOP(CancellationToken cancellationToken)
    {
        s_logger?.Trace("[NW.{Class}:{Action}] thread-started port={Port} priority={Priority}",
            nameof(TcpListenerBase), nameof(PROCESS_CHANNEL_LOOP), _port, Thread.CurrentThread.Priority);

        System.Threading.Channels.Channel<IConnection>? processChannel = _processChannel;
        if (processChannel is null)
        {
            return;
        }

        System.Threading.Channels.ChannelReader<IConnection> reader = processChannel.Reader;

        while (!cancellationToken.IsCancellationRequested)
        {
            // ── Fast path: drain all items available immediately ──────────────
            // TryRead() is not async -> does not consume overhead async state machine.
            // WHY fast-path first: In burst scenario, the channel can contain multiple items.
            // Drain runs out in the sync loop much faster than waiting each item individually.
            while (reader.TryRead(out IConnection? connection))
            {
                if (connection is null)
                {
                    continue;
                }

                this.INVOKE_PROCESS(connection);
            }

            // ── Slow path: wait for the next item (async) ─────────────────────────
            // WaitToReadAsync returns ValueTask<bool>:
            // - true -> has item -> continue loop to TryRead.
            // - false -> channel completed (shutdown) -> exit.
            ValueTask<bool> wait = reader.WaitToReadAsync(cancellationToken);

            if (wait.IsCompletedSuccessfully)
            {
                if (!wait.Result)
                {
                    break; // channel completed (shutdown)
                }

                continue;
            }

            // Genuinely async — block this thread until data is available.
            // WHY GetAwaiter().GetResult() instead of await:
            // - This is dedicated background thread (not async context).
            // - Blocking this thread is correct in design — this thread is SPECIFICALLY for waiting.
            // - Using await will create an unnecessary async state machine.
            try
            {
                if (!wait.AsTask().GetAwaiter().GetResult())
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Drain the remaining items after the channel is complete or the token has been cancelled.
        // WHY drain after exiting the loop:
        // - The channel may have the item written in before TryComplete() is called.
        // - No drain -> leak connection (socket not closed correctly).
        while (reader.TryRead(out IConnection? connection))
        {
            if (connection is null)
            {
                continue;
            }

            this.INVOKE_PROCESS(connection);
        }

        s_logger?.Trace("[NW.{Class}:{Action}] thread-exited port={Port}", nameof(TcpListenerBase), nameof(PROCESS_CHANNEL_LOOP), _port);
    }

    /// <summary>
    /// Calls <see cref="ProcessConnection"/> directly on the consumer thread —
    /// no additional ThreadPool hop needed.
    /// </summary>
    /// <param name="connection"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void INVOKE_PROCESS(IConnection connection)
    {
        try
        {
            // ProcessConnection -> OnAccept -> BeginReceive (completed in microseconds).
            // Receive loop actually runs async on ThreadPool -> no block consumer thread.
            // WHY calls directly instead of ThreadPool.Queue:
            // - Save 1 ThreadPool hop -> reduce latency.
            // - Consumer thread BelowNormal -> does not compete with I/O callbacks.
            this.ProcessConnection(connection);
        }
        catch (Exception ex)
        {
            s_logger?.Error(ex, "[NW.{Class}:{Action}] error remote={RemoteEndPoint} port={Port}",
                nameof(TcpListenerBase), nameof(INVOKE_PROCESS), connection?.NetworkEndpoint.ToString() ?? "<null>", _port);

            ArgumentNullException.ThrowIfNull(connection);
            connection.Close();
        }
    }

    #endregion Consumer — BelowNormal background thread
}
