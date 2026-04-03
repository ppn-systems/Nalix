// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal.Routing;

namespace Nalix.Network.Routing;

/// <summary>
/// High-performance packet dispatch channel with coalesced wake-up signaling.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("Running={_running}, Pending={_dispatch.TotalPackets}")]
public sealed class PacketDispatchChannel
    : PacketDispatcherBase<IPacket>, IPacketDispatch, IDisposable, IActivatable
{
    #region Fields

    private readonly IPacketRegistry _catalog;
    private readonly DispatchChannel<IPacket> _dispatch;
    private readonly Channel<byte> _wakeChannel;
    private readonly int _maxDrainPerWake;

    private int _running;
    private int _activeLoops;
    private int _dispatchLoops;
    private int _wakeRequested;

    private long _wakeSignals;
    private long _wakeReadSignals;

    private IWorkerHandle[] _workerHandle = [];
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _linkedCts;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchChannel"/> class.
    /// </summary>
    /// <param name="options">Option builder.</param>
    public PacketDispatchChannel(Action<PacketDispatchOptions<IPacket>> options) : base(options)
    {
        _catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
                   ?? throw new InternalErrorException(
                       $"[{nameof(PacketDispatchChannel)}] IPacketRegistry not registered in InstanceManager.");

        _dispatch = new DispatchChannel<IPacket>();
        _wakeChannel = Channel.CreateUnbounded<byte>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = false,
                SingleWriter = false
            });

        _maxDrainPerWake = Math.Clamp(Environment.ProcessorCount * 8, 64, 2048);
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Starts background dispatch workers.
    /// </summary>
    /// <param name="cancellationToken">External cancellation token.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        _linkedCts?.Dispose();
        _linkedCts = null;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        CancellationToken linkedToken;
        if (cancellationToken.CanBeCanceled)
        {
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
            linkedToken = _linkedCts.Token;
        }
        else
        {
            linkedToken = _cts.Token;
        }

        _ = Interlocked.Exchange(ref _wakeRequested, 0);
        _ = Interlocked.Exchange(ref _wakeSignals, 0);
        _ = Interlocked.Exchange(ref _wakeReadSignals, 0);
        _ = this.DrainWakeSignals();

        Volatile.Write(ref _activeLoops, 0);

        _dispatchLoops = this.Options.DispatchLoopCount ?? Math.Clamp(Environment.ProcessorCount, 1, 64);
        _workerHandle = new IWorkerHandle[_dispatchLoops];

        for (int i = 0; i < _dispatchLoops; i++)
        {
            _ = Interlocked.Increment(ref _activeLoops);

            _workerHandle[i] = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"{TaskNaming.Tags.Dispatch}.{TaskNaming.Tags.Process}.{i}",
                group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Dispatch}",
                work: async (ctx, ct) => await this.RunLoop(ctx, ct).ConfigureAwait(false),
                options: new WorkerOptions
                {
                    RetainFor = TimeSpan.Zero,
                    Tag = TaskNaming.Tags.Net,
                    IdType = SnowflakeType.System,
                    CancellationToken = linkedToken,
                });
        }

        this.RequestWake();
    }

    /// <summary>
    /// Stops dispatch workers.
    /// </summary>
    /// <param name="cancellationToken">Unused optional token for compatibility.</param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _running, 0) == 0)
        {
            return;
        }

        CancellationTokenSource? localCts = Interlocked.Exchange(ref _cts, null);
        CancellationTokenSource? linkedCts = Interlocked.Exchange(ref _linkedCts, null);

        try
        {
            for (int i = 0; i < _dispatchLoops; i++)
            {
                if (i < _workerHandle.Length && _workerHandle[i] is not null)
                {
                    InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_workerHandle[i].Id);
                }
            }

            if (localCts is { IsCancellationRequested: false })
            {
                localCts.Cancel();
            }

            int wakeCount = Math.Max(_dispatchLoops, 1);
            for (int i = 0; i < wakeCount; i++)
            {
                _ = _wakeChannel.Writer.TryWrite(0);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            try { linkedCts?.Dispose(); } catch { }
            try { localCts?.Dispose(); } catch { }
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IBufferLease packet, IConnection connection)
    {
        if (packet is null || connection is null)
        {
            packet?.Dispose();
            return;
        }

        if (packet.Length <= 0)
        {
            packet.Dispose();
            return;
        }

        if (_dispatch.PushCore(connection, packet))
        {
            this.RequestWake();
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HandlePacket(IPacket packet, IConnection connection)
        => this.ExecutePacketHandlerAsync(packet, connection).Await();

    #endregion Public Methods

    #region IReportable

    /// <summary>
    /// Generates a human-readable diagnostic report.
    /// </summary>
    /// <returns>Formatted report.</returns>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new(2048);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PacketDispatchChannel:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Running              : {(Volatile.Read(ref _running) == 1 ? "Yes" : "No")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DispatchLoops        : {_dispatchLoops}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"WakeSignals          : {Interlocked.Read(ref _wakeSignals)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"WakeReads            : {Interlocked.Read(ref _wakeReadSignals)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"WakeRequested        : {Volatile.Read(ref _wakeRequested)}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Channel Statistics:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Packets        : {_dispatch.TotalPackets}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Connections    : {_dispatch.TotalConnections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Ready Connections    : {_dispatch.ReadyConnections}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Pending by Priority:");
        _ = sb.AppendLine("Priority          | Pending Connections");
        _ = sb.AppendLine("------------------|---------------------");
        int[] priorities = _dispatch.PendingPerPriority;
        for (int p = priorities.Length - 1; p >= 0; p--)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{GetPriorityName(p),-18}| {priorities[p],-19}");
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine("Top Connections by Pending Packets:");
        _ = sb.AppendLine("EndPoint              | Pending");
        _ = sb.AppendLine("----------------------|----------");

        List<KeyValuePair<IConnection, int>> ranked = [.. _dispatch.PendingPerConnection];
        ranked.Sort(static (a, b) => b.Value.CompareTo(a.Value));

        int top = Math.Min(10, ranked.Count);
        for (int i = 0; i < top; i++)
        {
            KeyValuePair<IConnection, int> kv = ranked[i];
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{kv.Key.NetworkEndpoint,-22}| {kv.Value,6}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic snapshot.
    /// </summary>
    /// <returns>Diagnostic dictionary.</returns>
    public IDictionary<string, object> GenerateReportData()
    {
        Dictionary<string, object> report = new()
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["Running"] = Volatile.Read(ref _running) == 1,
            ["DispatchLoops"] = _dispatchLoops,
            ["TotalPackets"] = _dispatch.TotalPackets,
            ["TotalConnections"] = _dispatch.TotalConnections,
            ["ReadyConnections"] = _dispatch.ReadyConnections,
            ["WakeSignals"] = Interlocked.Read(ref _wakeSignals),
            ["WakeReads"] = Interlocked.Read(ref _wakeReadSignals),
            ["WakeRequested"] = Volatile.Read(ref _wakeRequested),
            ["PacketRegistryType"] = _catalog.GetType().Name
        };

        int[] priorities = _dispatch.PendingPerPriority;
        Dictionary<string, int> pendingPerPriority = new(priorities.Length);
        for (int p = priorities.Length - 1; p >= 0; p--)
        {
            pendingPerPriority[GetPriorityName(p)] = priorities[p];
        }

        report["PendingPerPriority"] = pendingPerPriority;

        List<KeyValuePair<IConnection, int>> ranked = [.. _dispatch.PendingPerConnection];
        ranked.Sort(static (a, b) => b.Value.CompareTo(a.Value));

        int top = Math.Min(10, ranked.Count);
        List<Dictionary<string, object>> topConnections = new(top);
        for (int i = 0; i < top; i++)
        {
            KeyValuePair<IConnection, int> kv = ranked[i];
            topConnections.Add(new Dictionary<string, object>
            {
                ["EndPoint"] = kv.Key.NetworkEndpoint.Address,
                ["Pending"] = kv.Value
            });
        }

        report["PendingByConnection"] = topConnections;
        return report;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetPriorityName(int index)
    {
        string? value = Enum.GetName(typeof(PacketPriority), index);
        return value ?? index.ToString(CultureInfo.InvariantCulture);
    }

    #endregion IReportable

    #region Private Methods

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async Task RunLoop(IWorkerContext ctx, CancellationToken ct)
    {
        try
        {
            while (Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                ctx.Beat();

                if (!await _wakeChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    continue;
                }

                int wakes = this.DrainWakeSignals();
                if (wakes > 0)
                {
                    _ = Interlocked.Add(ref _wakeReadSignals, wakes);
                }

                _ = Interlocked.Exchange(ref _wakeRequested, 0);

                int processed = 0;
                while (processed < _maxDrainPerWake &&
                       _dispatch.Pull(out IConnection? connection, out IBufferLease? lease))
                {
                    ValueTask pending = this.ProcessOneAsync(connection, lease, ct);
                    if (pending.IsCompletedSuccessfully)
                    {
                        pending.GetAwaiter().GetResult();
                    }
                    else
                    {
                        await pending.ConfigureAwait(false);
                    }

                    processed++;
                }

                if (processed > 0)
                {
                    ctx.Advance(processed);
                }

                if (_dispatch.HasPacket)
                {
                    this.RequestWake();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] loop-error", ex);
        }
        finally
        {
            if (Interlocked.Decrement(ref _activeLoops) == 0)
            {
                Volatile.Write(ref _running, 0);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private ValueTask ProcessOneAsync(IConnection connection, IBufferLease lease, CancellationToken ct)
    {
        ValueTask<IBufferLease?> pipelinePending = this.Options.NetworkPipeline.ExecuteAsync(lease, connection, ct);
        if (pipelinePending.IsCompletedSuccessfully)
        {
            IBufferLease? effectiveLease;
            try
            {
                effectiveLease = pipelinePending.Result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                connection.IncrementErrorCount();
                this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(ProcessOneAsync)}] pipeline-error ep={connection.NetworkEndpoint}", ex);
                return ValueTask.CompletedTask;
            }

            if (effectiveLease is null)
            {
                return ValueTask.CompletedTask;
            }

            return this.ProcessResolvedLease(connection, effectiveLease, ct);
        }

        return AwaitPipelineAsync(this, connection, ct, pipelinePending);

        static async ValueTask AwaitPipelineAsync(
            PacketDispatchChannel owner,
            IConnection connection,
            CancellationToken ct,
            ValueTask<IBufferLease?> pending)
        {
            IBufferLease? effectiveLease = null;
            try
            {
                effectiveLease = await pending.ConfigureAwait(false);
                if (effectiveLease is null)
                {
                    return;
                }

                await owner.ProcessResolvedLease(connection, effectiveLease, ct).ConfigureAwait(false);
                effectiveLease = null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                connection.IncrementErrorCount();
                owner.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(ProcessOneAsync)}] pipeline-error ep={connection.NetworkEndpoint}", ex);
            }
            finally
            {
                effectiveLease?.Dispose();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private ValueTask ProcessResolvedLease(IConnection connection, IBufferLease lease, CancellationToken ct)
    {
        try
        {
            if (!_catalog.TryDeserialize(lease.Span, out IPacket? packet) || packet is null)
            {
                connection.IncrementErrorCount();
                lease.Dispose();
                return ValueTask.CompletedTask;
            }

            ValueTask dispatchPending = this.ExecutePacketHandlerAsync(packet, connection, ct);
            if (dispatchPending.IsCompletedSuccessfully)
            {
                try
                {
                    dispatchPending.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    connection.IncrementErrorCount();
                    this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(ProcessOneAsync)}] handler-error ep={connection.NetworkEndpoint}", ex);
                }
                finally
                {
                    lease.Dispose();
                }

                return ValueTask.CompletedTask;
            }

            return AwaitDispatchAsync(this, connection, lease, dispatchPending, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            lease.Dispose();
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            connection.IncrementErrorCount();
            this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(ProcessOneAsync)}] handler-error ep={connection.NetworkEndpoint}", ex);
            lease.Dispose();
            return ValueTask.CompletedTask;
        }

        static async ValueTask AwaitDispatchAsync(
            PacketDispatchChannel owner,
            IConnection connection,
            IBufferLease lease,
            ValueTask pending,
            CancellationToken ct)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                connection.IncrementErrorCount();
                owner.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(ProcessOneAsync)}] handler-error ep={connection.NetworkEndpoint}", ex);
            }
            finally
            {
                lease.Dispose();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RequestWake()
    {
        if (Interlocked.Exchange(ref _wakeRequested, 1) != 0)
        {
            return;
        }

        if (_wakeChannel.Writer.TryWrite(0))
        {
            _ = Interlocked.Increment(ref _wakeSignals);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DrainWakeSignals()
    {
        int drained = 0;
        while (_wakeChannel.Reader.TryRead(out _))
        {
            drained++;
        }

        return drained;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases dispatcher resources.
    /// </summary>
    [DebuggerNonUserCode]
    public void Dispose()
    {
        this.Deactivate();
        _linkedCts?.Dispose();
        _cts?.Dispose();
    }

    #endregion IDisposable
}
