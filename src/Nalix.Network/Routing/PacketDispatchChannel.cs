// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal.Constants;
using Nalix.Network.Routing.Channel;

namespace Nalix.Network.Routing;

/// <summary>
/// Represents an ultra-high performance lease dispatcher designed for asynchronous, queue-based processing
/// with dependency injection (DI) support and flexible lease handling via reflection-based routing.
/// </summary>
/// <remarks>
/// <para>
/// This dispatcher works by queuing incoming packets and processing them in a background loop. Packet handling
/// is done asynchronously using handlers resolved via lease command IDs.
/// </para>
/// <para>
/// It is suitable for high-throughput systems such as custom RELIABLE servers, IoT message brokers, or game servers
/// where latency, memory pressure, and throughput are critical.
/// </para>
/// </remarks>
/// <example>
/// Example usage:
/// <code>
/// var dispatcher = new PacketDispatchChannel`Packet`(opts => {
///     opts.WithHandler(...);
/// });
/// ...
/// dispatcher.HandlePacket(data, connection);
/// </code>
/// </example>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("Running={_running}, Pending={_dispatch.TotalPackets}")]
public sealed class PacketDispatchChannel
    : PacketDispatcherBase<IPacket>, IPacketDispatch, IDisposable, IActivatable
{
    #region Fields

    private readonly IPacketRegistry _catalog;
    private readonly DispatchChannel<IPacket> _dispatch;
    private readonly SemaphoreSlim _semaphore = new(0);

    private int _running;
    private int _activeLoops;
    private int _dispatchLoops;
    private IWorkerHandle[] _workerHandle = [];
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _linkedCts;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchChannel"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketDispatchChannel(Action<PacketDispatchOptions<IPacket>> options) : base(options)
    {
        _dispatch = new DispatchChannel<IPacket>();
        _catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
                   ?? throw new InvalidOperationException(
                       $"[{nameof(PacketDispatchChannel)}] IPacketRegistry not registered in InstanceManager. Make sure to build and register IPacketRegistry before starting dispatcher.");

        // Push any additional initialization here if needed
        this.Logging?.Debug($"[{nameof(PacketDispatchChannel)}] init");
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Starts the lease processing loop
    /// </summary>
    /// <param name="cancellationToken"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            this.Logging?.Debug($"[{nameof(PacketDispatchChannel)}:{this.Activate}] already-running");
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

        Volatile.Write(ref _activeLoops, 0);

        // Decide how many parallel dispatch loops to start.
        // Rule of thumb: cores/2, clamped to [1..12]
        _dispatchLoops = this.Options.DispatchLoopCount ?? Math.Clamp(Environment.ProcessorCount / 2, 1, 12);
        _workerHandle = new IWorkerHandle[_dispatchLoops];

        for (int i = 0; i < _dispatchLoops; i++)
        {
            _ = Interlocked.Increment(ref _activeLoops);

            _workerHandle[i] = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"{TaskNaming.Tags.Dispatch}.{TaskNaming.Tags.Process}.{i}",
                group: $"{NetworkTags.Net}/{TaskNaming.Tags.Dispatch}",
                work: async (ctx, ct) => await this.RunLoop(ctx, ct).ConfigureAwait(false),
                options: new WorkerOptions
                {
                    IdType = SnowflakeType.System,
                    CancellationToken = linkedToken,
                    RetainFor = TimeSpan.Zero,
                    Tag = NetworkTags.Net
                }
            );
        }

        this.Logging?.Trace($"[{nameof(PacketDispatchChannel)}:{this.Activate}] start");
    }

    /// <summary>
    /// Stops the lease processing loop
    /// </summary>
    /// <param name="cancellationToken"></param>
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
                    _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                                .CancelWorker(_workerHandle[i].Id);
                }
            }

            if (localCts is { IsCancellationRequested: false })
            {
                localCts.Cancel();
                this.Logging?.Trace($"[{nameof(PacketDispatchChannel)}:{this.Deactivate}] stop");
            }

            try
            {
                int releases = Math.Max(_dispatchLoops, 1);
                for (int i = 0; i < releases; i++)
                {
                    _ = _semaphore.Release();
                }
            }
            catch { /* ignore over-release */ }
        }
        catch (ObjectDisposedException)
        {
            this.Logging?.Warn($"[{nameof(PacketDispatchChannel)}:{this.Deactivate}] stop-on-disposed-cts");
        }
        catch (Exception ex)
        {
            this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{this.Deactivate}] stop-error", ex);
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
        ArgumentNullException.ThrowIfNull(packet, nameof(packet));
        ArgumentNullException.ThrowIfNull(connection, nameof(connection));

        if (packet is null || packet.Length <= 0)
        {
            this.Logging?.Debug($"[{nameof(PacketDispatchChannel)}:{nameof(HandlePacket)}] empty-payload ep={connection.NetworkEndpoint}");
            packet?.Dispose();

            return;
        }

        // Enqueue lease into the priority-aware channel (per-connection).
        _dispatch.Push(connection, packet);

        // Signal the worker that an item is available.
        _ = _semaphore.Release();
    }

    /// <inheritdoc />
    // If you want typed fast-path, you can implement a separate typed channel.
    // For now, process immediately to avoid mixing typed/lease queues.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HandlePacket(IPacket packet, IConnection connection) => this.ExecutePacketHandlerAsync(packet, connection).Await();

    #endregion Public Methods

    #region IReportable

    /// <summary>
    /// Generates a human-readable diagnostic report for the PacketDispatchChannel.
    /// </summary>
    /// <returns>A formatted report string.</returns>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new(2048);

        // Header
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PacketDispatchChannel:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Running           : {(Volatile.Read(ref _running) == 1 ? "Yes" : "No")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DispatchLoops     : {_dispatchLoops}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Channel Statistics:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Packets     : {_dispatch.TotalPackets}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Connections : {_dispatch.TotalConnections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Ready Connections : {_dispatch.ReadyConnections}");
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
        foreach (KeyValuePair<IConnection, int> kv in _dispatch.PendingPerConnection.OrderByDescending(x => x.Value).Take(10))
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{kv.Key.NetworkEndpoint,-22}| {kv.Value,6}");
        }

        _ = sb.AppendLine();

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Resources / Metrics:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Semaphore.CurrentCount: {_semaphore.CurrentCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CTS.Cancelled         : {_cts?.IsCancellationRequested ?? false}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Packet Registry:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Registry Type         : {_catalog.GetType().Name}");
        _ = sb.AppendLine();

        // Optionally list registered handlers if available in _catalog
        // sb.AppendLine("Registered handlers   : ...");

        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Quick Notes:");
        _ = sb.AppendLine("• TotalPackets        = total packets pending for processing");
        _ = sb.AppendLine("• ReadyConnections    = number of connections with packets ready for processing");
        _ = sb.AppendLine("• PendingPerPriority  = number of ready connections by priority");
        _ = sb.AppendLine("• Top Connections     = endpoints with the most pending packets");
        _ = sb.AppendLine("• Resources/Memory    = system resource statistics");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic summary of the dispatcher state and per-channel statistics.
    /// </summary>
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
            ["Semaphore.CurrentCount"] = _semaphore.CurrentCount,
            ["CTS.Cancelled"] = _cts?.IsCancellationRequested ?? false,
            ["PacketRegistryType"] = _catalog.GetType().Name
        };

        // Pending by priority
        int[] priorities = _dispatch.PendingPerPriority;
        Dictionary<string, int> pendingPerPriority = new(priorities.Length);
        for (int p = priorities.Length - 1; p >= 0; p--)
        {
            pendingPerPriority[GetPriorityName(p)] = priorities[p];
        }
        report["PendingPerPriority"] = pendingPerPriority;

        // Top connections by pending packets
        report["PendingByConnection"] = _dispatch.PendingPerConnection
            .OrderByDescending(x => x.Value)
            .Take(10)
            .Select(kv => new Dictionary<string, object>
            {
                ["EndPoint"] = kv.Key.NetworkEndpoint.Address,
                ["Pending"] = kv.Value
            })
            .ToList();

        return report;
    }

    private static string GetPriorityName(int index)
    {
        try
        {
            return Enum.GetName(typeof(PacketPriority), index) ?? index.ToString(CultureInfo.InvariantCulture);
        }
        catch
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }
    }

    #endregion IReportable

    #region Private Methods

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="ct"></param>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private async Task RunLoop(IWorkerContext ctx, CancellationToken ct)
    {
        TimeSpan heartbeatInterval = TimeSpan.FromSeconds(1);

        try
        {
            while (Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                ctx.Beat();

                bool signaled = await _semaphore.WaitAsync(heartbeatInterval, ct).ConfigureAwait(false);
                if (!signaled)
                {
                    continue;
                }

                // Pull from channel (priority-aware)
                if (!_dispatch.Pull(out IConnection? connection, out IBufferLease? lease))
                {
                    if (!_dispatch.HasPacket)
                    {
                        continue;
                    }

                    this.Logging?.Trace($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] pull-empty");
                    lease?.Dispose();
                    lease = null;
                    continue;
                }

                try
                {
                    IBufferLease? afterMw = await this.Options.NetworkPipeline.ExecuteAsync(lease, connection, ct).ConfigureAwait(false);

                    if (afterMw is null)
                    {
                        this.Logging?.Debug($"[PacketDispatchChannel:RunLoop] middleware-reject ep={connection.NetworkEndpoint}");
                        lease.Dispose();
                        lease = null;
                        continue;
                    }

                    if (afterMw != lease)
                    {
                        lease.Dispose();
                        lease = afterMw;
                    }
                }
                catch (Exception ex)
                {
                    connection.IncrementErrorCount();
                    this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] buffer-middleware-error ep={connection.NetworkEndpoint} leaseLen={lease?.Length}", ex);

                    lease?.Dispose();
                    lease = null;
                    continue;
                }

                try
                {
                    // Deserialize packet
                    if (!_catalog.TryDeserialize(lease.Span, out IPacket? packet) || packet is null)
                    {
                        int len = lease.Length;
                        string head = Convert.ToHexString(lease.Span[..Math.Min(16, len)]);
                        this.Logging?.Warn($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] deserialize-none ep={connection.NetworkEndpoint} len={len} head={head}");

                        lease.Dispose();
                        lease = null;
                        continue;
                    }

                    await this.ExecutePacketHandlerAsync(packet, connection).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    connection.IncrementErrorCount();
                    this.Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] handle-error ep={connection.NetworkEndpoint}", ex);
                }
                finally
                {
                    lease?.Dispose();
                    lease = null;
                }

                ctx.Advance(1);
            }
        }
        catch (OperationCanceledException)
        {
            // NONE cancellation, no need to log
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

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    [DebuggerNonUserCode]
    public void Dispose()
    {
        this.Deactivate();

        _linkedCts?.Dispose();
        _cts?.Dispose();
        _semaphore.Dispose();
    }

    #endregion IDisposable
}
