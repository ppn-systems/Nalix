// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    #region Fields

    // Thread BelowNormal is separate so that drain channel — is separate from ThreadPool.
    // WHY separate thread: This thread runs continuously; using ThreadPool will occupy the thread pool
    // and compete with more important async callback I/O.
    private System.Threading.Channels.Channel<IConnection>? _processChannel;
    private IWorkerHandle? _processWorker;

    #endregion Fields

    #region Lifecycle

    /// <summary>
    /// Creates the channel and starts the consumer worker.
    /// Call once during listener startup inside <c>Activate</c>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private void START_PROCESS_CHANNEL(CancellationToken cancellationToken)
    {
        _processChannel = System.Threading.Channels.Channel.CreateBounded<IConnection>(
            new System.Threading.Channels.BoundedChannelOptions(_config.ProcessChannelCapacity)
            {
                SingleReader = true,   // only the consumer worker reads
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

        _processWorker = Framework.Injection.InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{TaskNaming.Tags.Tcp}.{TaskNaming.Tags.Accept}.{TaskNaming.Tags.Dispatch}.{_port}",
            group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Tcp}/{_port}",
            work: this.PROCESS_CHANNEL_LOOP_ASYNC,
            options: new WorkerOptions
            {
                // SEC-DDOS: Dedicated thread BelowNormal priority -> OS scheduler prioritizes Normal-priority ThreadPool threads.
                // This ensures I/O callbacks always win CPU time over new connection accepting during saturation.
                OSPriority = ThreadPriority.BelowNormal,
                Tag = TaskNaming.Tags.Net,
                IdType = SnowflakeType.System,
                RetainFor = TimeSpan.Zero,
                CancellationToken = cancellationToken
            });
    }

    /// <summary>
    /// Completes the channel and waits for the consumer worker to exit.
    /// Call during listener shutdown inside <c>Deactivate</c>.
    /// </summary>
    private void STOP_PROCESS_CHANNEL()
    {
        // TryComplete() -> marks the channel as "closed" (will not accept any new items).
        // The consumer worker will drain the remaining items and then exit the loop.
        _ = (_processChannel?.Writer.TryComplete());

        IWorkerHandle? worker = Interlocked.Exchange(ref _processWorker, null);

        if (worker != null)
        {
            Framework.Injection.InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(worker.Id);

            int elapsed = 0;
            int timeout = _config.ProcessChannelDrainTimeout;
            while (worker.IsRunning && elapsed < timeout)
            {
                Thread.Sleep(10);
                elapsed += 10;
            }

            worker.Dispose();
            _processWorker = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsProcessChannelFull()
    {
        System.Threading.Channels.Channel<IConnection>? ch = _processChannel;
        return ch != null && ch.Reader.CanCount && ch.Reader.Count >= _config.ProcessChannelCapacity;
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
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(DISPATCH_CONNECTION)}] " +
                                 $"process-channel-unavailable remote={connection?.NetworkEndpoint.ToString() ?? "<null>"} port={_port}");
            }

            ArgumentNullException.ThrowIfNull(connection);
            connection.Dispose();
            return;
        }

        if (processChannel.Writer.TryWrite(connection))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(DISPATCH_CONNECTION)}] " +
                    $"queued remote={connection?.NetworkEndpoint.ToString() ?? "<null>"} port={_port}");
            }

            return;
        }

        // Channel full (FullMode = DropWrite) -> drop new connection.
        // WHY drop new instead of drop old:
        // - The old connection in the channel has been accepted and may be legitimate user.
        // - The new connection (dropped) could be part of a DDoS burst -> drop is correct.
        // - Backpressure signal: If the channel is consistently full, increase ProcessChannelCapacity or
        //   optimize ProcessConnection for faster execution.
        this.Metrics.RECORD_REJECTED();

        if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(DISPATCH_CONNECTION)}] " +
                             $"channel-full remote={connection?.NetworkEndpoint.ToString() ?? "<null>"} port={_port} - dropped");
        }

        ArgumentNullException.ThrowIfNull(connection);
        connection.Disconnect();
    }

    #endregion Producer — called from AcceptConnectionsAsync

    #region Consumer — BelowNormal background thread

    private async ValueTask PROCESS_CHANNEL_LOOP_ASYNC(IWorkerContext ctx, CancellationToken cancellationToken)
    {
        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                $"[NW.{nameof(TcpListenerBase)}:{nameof(PROCESS_CHANNEL_LOOP_ASYNC)}] " +
                $"worker-started port={_port}");
        }

        System.Threading.Channels.Channel<IConnection>? processChannel = _processChannel;
        if (processChannel is null)
        {
            return;
        }

        System.Threading.Channels.ChannelReader<IConnection> reader = processChannel.Reader;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ctx.Beat();

                // ── Fast path: drain all items available immediately ──────────────
                // TryRead() is not async -> does not consume overhead async state machine.
#pragma warning disable CA2000 // Channel yields an owned connection; ownership is transferred to INVOKE_PROCESS/ProcessConnection.
                while (reader.TryRead(out IConnection? connection))
                {
                    if (connection is null)
                    {
                        continue;
                    }

                    this.INVOKE_PROCESS(connection);
                    ctx.Advance(1);
                }
#pragma warning restore CA2000

                // ── Slow path: wait for the next item (async) ─────────────────────────
                // Genuinely async — wait for data without blocking.
                if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(TcpListenerBase)}:{nameof(PROCESS_CHANNEL_LOOP_ASYNC)}] unhandled-error port={_port}");
            }
        }
        finally
        {
            // Drain the remaining items
            while (reader.TryRead(out IConnection? connection))
            {
                if (connection is null)
                {
                    continue;
                }

                this.INVOKE_PROCESS(connection);
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"[NW.{nameof(TcpListenerBase)}:{nameof(PROCESS_CHANNEL_LOOP_ASYNC)}] worker-exited port={_port}");
            }
        }
    }

    /// <summary>
    /// Calls <see cref="ProcessConnection"/> directly on the consumer worker —
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
            // - Dedicated worker thread BelowNormal -> does not compete with I/O callbacks.
            this.ProcessConnection(connection);
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            this.Metrics.RECORD_ERROR();

            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(INVOKE_PROCESS)}] " +
                    $"error remote={connection?.NetworkEndpoint.ToString() ?? "<null>"} port={_port}");
            }

            ArgumentNullException.ThrowIfNull(connection);
            connection.Disconnect();
        }
    }

    #endregion Consumer — BelowNormal background thread
}
