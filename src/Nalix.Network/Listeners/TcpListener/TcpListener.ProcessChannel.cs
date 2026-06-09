// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

#pragma warning disable IDE0079
#pragma warning disable CA2213

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
        if (_proxyConfig.Enabled)
        {
            this.START_PROXY_SWEEP(cancellationToken);
        }

        _processChannel = System.Threading.Channels.Channel.CreateBounded<IConnection>(
            new System.Threading.Channels.BoundedChannelOptions(_config.ProcessChannelCapacity)
            {
                SingleReader = true,   // only the consumer worker reads
                SingleWriter = false,  // many accept-loop workers write

                // Wait + TryWrite gives a non-blocking failure when the channel is full.
                // This lets us drop and close the new connection explicitly below.
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,

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
        if (_proxyConfig.Enabled)
        {
            this.STOP_PROXY_SWEEP();
        }

        // TryComplete() -> marks the channel as "closed" (will not accept any new items).
        // The consumer worker will drain the remaining items and then exit the loop.
        _ = (_processChannel?.Writer.TryComplete());

        IWorkerHandle? worker = Interlocked.Exchange(ref _processWorker, null);

        if (worker != null)
        {
            worker.Dispose();

            int elapsed = 0;
            int timeout = _config.ProcessChannelDrainTimeout;
            while (worker.IsRunning && elapsed < timeout)
            {
                Thread.Sleep(10);
                elapsed += 10;
            }
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
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning,
                    new DiagnosticLog("NW.TcpListenerBase:Internal", $"process-channel-unavailable remote-endpoint={connection?.NetworkEndpoint.ToString() ?? "<null>"}"));
            }

            ArgumentNullException.ThrowIfNull(connection);
            connection.Dispose();
            return;
        }

        if (processChannel.Writer.TryWrite(connection))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace,
                    new DiagnosticLog("NW.TcpListenerBase:Internal", $"queued remote-endpoint={connection?.NetworkEndpoint.ToString() ?? "<null>"}"));
            }

            return;
        }

        // Channel full (FullMode = Wait + TryWrite) -> drop new connection.
        // WHY drop new instead of drop old:
        // - The old connection in the channel has been accepted and may be legitimate user.
        // - The new connection (dropped) could be part of a DDoS burst -> drop is correct.
        // - Backpressure signal: If the channel is consistently full, increase ProcessChannelCapacity or
        //   optimize ProcessConnection for faster execution.
        this.Metrics.RECORD_QUEUE_FULL_REJECTION();

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning,
                new DiagnosticLog("NW.TcpListenerBase:Internal", $"channel-full remote-endpoint={connection?.NetworkEndpoint.ToString() ?? "<null>"}"));
        }

        ArgumentNullException.ThrowIfNull(connection);
        connection.Disconnect();
    }

    #endregion Producer — called from AcceptConnectionsAsync

    #region Consumer — BelowNormal background thread

    private async ValueTask PROCESS_CHANNEL_LOOP_ASYNC(IWorkerContext ctx, CancellationToken cancellationToken)
    {
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace,
                new DiagnosticLog("NW.TcpListenerBase:Internal", $"worker-started port={_port}"));
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
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.LoopFaulted,
                    new DiagnosticLog("NW.TcpListenerBase:Internal", $"unhandled-error port={_port}", ex));
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

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace,
                    new DiagnosticLog("NW.TcpListenerBase:Internal", $"worker-exited port={_port}"));
            }
        }
    }

    /// <summary>
    /// Calls <see cref="ProcessConnection"/> directly on the consumer worker —
    /// no additional ThreadPool hop needed.
    /// </summary>
    /// <param name="connection"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
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
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.Metrics.RECORD_ERROR();

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.LoopFaulted,
                    new DiagnosticLog("NW.TcpListenerBase:Internal", $"error remote-endpoint={connection.NetworkEndpoint.ToString() ?? "<null>"}", ex));
            }

            connection.Disconnect();
        }
    }

    #endregion Consumer — BelowNormal background thread
}
