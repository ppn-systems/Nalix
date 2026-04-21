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
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Routing;
using Nalix.Runtime.Internal.Routing;

namespace Nalix.Runtime.Dispatching;

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

        _maxDrainPerWake = Math.Clamp(Environment.ProcessorCount * this.Options.MaxDrainPerWakeMultiplier, this.Options.MinDrainPerWake, this.Options.MaxDrainPerWake);
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

        _dispatchLoops = this.Options.DispatchLoopCount ?? Math.Clamp(Environment.ProcessorCount, this.Options.MinDispatchLoops, this.Options.MaxDispatchLoops);
        _workerHandle = new IWorkerHandle[_dispatchLoops];
        CancellationToken linkedTokenRef = linkedToken;

        for (int i = 0; i < _dispatchLoops; i++)
        {
            _ = Interlocked.Increment(ref _activeLoops);

            int loopIndex = i;
            _workerHandle[i] = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"{TaskNaming.Tags.Dispatch}.{TaskNaming.Tags.Process}.{i}",
                group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Dispatch}",
                work: (ctx, ct) =>
                {
                    // This is the TaskManager worker thread (TaskPool).
                    using ManualResetEventSlim completion = new(false);

                    Thread osThread = new Thread(() =>
                    {
                        try
                        {
                            this.RunLoopSync(ctx, ct, loopIndex);
                        }
                        finally
                        {
                            completion.Set();
                        }
                    })
                    {
                        IsBackground = true,
                        Name = $"{TaskNaming.Tags.Dispatch}.{TaskNaming.Tags.Process}.{loopIndex}",
                        Priority = ThreadPriority.Normal
                    };

                    osThread.Start();

                    // Keep the TaskManager worker alive and beating while the OS thread works.
                    // This satisfies both Dedicated Thread performance and TaskManager observability.
                    try
                    {
                        while (!completion.Wait(3000, ct))
                        {
                            ctx.Beat();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Signal received from TaskManager or external source.
                    }

                    return ValueTask.CompletedTask;
                },
                options: new WorkerOptions
                {
                    RetainFor = TimeSpan.Zero,
                    Tag = TaskNaming.Tags.Net,
                    IdType = SnowflakeType.System,
                    CancellationToken = linkedTokenRef,
                    Priority = WorkerPriority.HIGH
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
            return;
        }

        if (packet.Length <= 0)
        {
            return;
        }

        // Industrial-grade reference management: we must retain the lease before
        // handoff to the asynchronous dispatch channel. If we don't, the caller
        // (the transport layer) will dispose its reference immediately after this
        // call returns, potentially returning the buffer to the pool while we
        // are still processing it.
        packet.Retain();

        if (!_dispatch.PushCore(connection, packet, noBlock: true))
        {
            // If the channel is full or the connection is inactive, we must
            // release the reference we just took to avoid a memory leak.
            packet.Dispose();
            return;
        }

        // Signal a worker to wake up and process the newly queued packet.
        this.RequestWake();
    }

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
    public IDictionary<string, object> GetReportData()
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
    private void RunLoopSync(IWorkerContext ctx, CancellationToken ct, int index)
    {
        // THIS IS THE DEDICATED OS THREAD.
        // Performance is maximized by omitting async state machines.

        try
        {
            ChannelReader<byte> reader = _wakeChannel.Reader;

            while (Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                ctx.Beat();

                ValueTask<bool> wait = reader.WaitToReadAsync(ct);
                if (wait.IsCompletedSuccessfully)
                {
                    if (!wait.Result)
                    {
                        break;
                    }
                }
                else
                {
                    try
                    {
                        if (!wait.AsTask().GetAwaiter().GetResult())
                        {
                            break;
                        }
                    }
                    catch (OperationCanceledException) { break; }
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
                    ValueTask pending = this.DispatchLeaseAsync(connection, lease, ct);

                    if (pending.IsCompletedSuccessfully)
                    {
                        pending.GetAwaiter().GetResult();
                    }
                    else
                    {
                        pending.AsTask().GetAwaiter().GetResult();
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
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoopSync)}] loop-error index={index}", ex);
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
    private ValueTask DispatchLeaseAsync(IConnection connection, IBufferLease lease, CancellationToken ct)
    {
        // 1. Acquire pooled packet via registry (zero alloc)
        // If TryDeserializePooled fails, it has already returned any partially-deserialized pooled objects.
        if (!_catalog.TryDeserializePooled(lease.Span, out IPacket? packet) || packet is null)
        {
            connection.IncrementErrorCount();
            lease.Dispose();
            return ValueTask.CompletedTask;
        }

        try
        {
            // 2. Execute packet handler
            ValueTask pending = this.ExecutePacketHandlerAsync(packet, connection, ct);

            // 3. Fast-path: handler completed synchronously
            if (pending.IsCompletedSuccessfully)
            {
                pending.GetAwaiter().GetResult();
                _catalog.ReturnPacket(packet);
                lease.Dispose();
                return ValueTask.CompletedTask;
            }

            // 4. Slow-path: async completion (AwaitDispatchAsync handles Return/Dispose)
            return AwaitDispatchAsync(this, connection, lease, packet, pending, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // External cancellation during sync execution
        }
        catch (Exception ex)
        {
            connection.IncrementErrorCount();
            this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(DispatchLeaseAsync)}] handler-error ep={connection.NetworkEndpoint}", ex);
        }

        // 5. Cleanup for synchronous errors/cancellation
        _catalog.ReturnPacket(packet);
        lease.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async ValueTask AwaitDispatchAsync(
        PacketDispatchChannel owner,
        IConnection connection,
        IBufferLease lease,
        IPacket packet,
        ValueTask pending,
        CancellationToken ct)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Async cancellation
        }
        catch (Exception ex)
        {
            connection.IncrementErrorCount();
            owner.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(DispatchLeaseAsync)}] handler-error ep={connection.NetworkEndpoint}", ex);
        }
        finally
        {
            // Guaranteed release for async path
            owner._catalog.ReturnPacket(packet);
            lease.Dispose();
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
