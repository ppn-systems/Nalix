// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    #region Fields

    /// <summary>
    /// Dedicated consumer thread — drains the channel at BelowNormal priority.
    /// </summary>
    private Thread _processThread;

    /// <summary>
    /// Bounded channel: N producers (accept-loop workers) → 1 consumer (BelowNormal thread).
    /// </summary>
    private System.Threading.Channels.Channel<IConnection> _processChannel;

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
                // DropWrite = TryWrite returns false when full (no blocking producer)
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropWrite,
                AllowSynchronousContinuations = false,
            });

        _processThread = new Thread(() => PROCESS_CHANNEL_LOOP(cancellationToken))
        {
            IsBackground = true,
            Name = $"NW.AcceptDispatch.{_port}",
            // ── Core of the priority fix ─────────────────────────────────────
            // OS scheduler prefers Normal-priority ThreadPool threads over this
            // thread whenever both are runnable.
            // → AsyncCallback packet callbacks always win CPU over new-connection setup.
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
        _ = (_processChannel?.Writer.TryComplete());
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
        if (_processChannel.Writer.TryWrite(connection))
        {
            s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(DISPATCH_CONNECTION)}] queued remote={connection?.NetworkEndpoint} port={_port}");
            return;
        }

        // Channel full → DDoS backpressure: drop the new connection immediately.
        // Existing legitimate connections already in the channel are unaffected.
        Metrics.RECORD_REJECTED();
        s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(DISPATCH_CONNECTION)}] channel-full remote={connection?.NetworkEndpoint} port={_port} — dropped");

        connection.Close();
    }

    #endregion Producer — called from AcceptConnectionsAsync

    #region Consumer — BelowNormal background thread

    private void PROCESS_CHANNEL_LOOP(CancellationToken cancellationToken)
    {
        s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(PROCESS_CHANNEL_LOOP)}] " +
                        $"thread-started port={_port} priority={Thread.CurrentThread.Priority}");

        System.Threading.Channels.ChannelReader<IConnection> reader = _processChannel.Reader;

        while (!cancellationToken.IsCancellationRequested)
        {
            // ── Fast path: drain all immediately available items ──────────────
            // Avoids async state machine overhead during bursts.
            while (reader.TryRead(out IConnection connection))
            {
                INVOKE_PROCESS(connection);
            }

            // ── Slow path: wait for next item ─────────────────────────────────
            ValueTask<bool> wait = reader.WaitToReadAsync(cancellationToken);

            if (wait.IsCompletedSuccessfully)
            {
                if (!wait.Result)
                {
                    break; // channel completed (shutdown)
                }

                continue;
            }

            // Genuinely async — block this background thread until data arrives.
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

        // Drain any remaining items that arrived before the channel was completed.
        while (reader.TryRead(out IConnection connection))
        {
            INVOKE_PROCESS(connection);
        }

        s_logger.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(PROCESS_CHANNEL_LOOP)}] thread-exited port={_port}");
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
            // ProcessConnection → OnAccept → BeginReceive (returns in microseconds).
            // The receive loop itself runs as a separate async task on the ThreadPool.
            ProcessConnection(connection);
        }
        catch (Exception ex)
        {
            s_logger.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(INVOKE_PROCESS)}] error remote={connection?.NetworkEndpoint} port={_port}", ex);
            connection.Close();
        }
    }

    #endregion Consumer — BelowNormal background thread
}
