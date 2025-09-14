// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal;
using Nalix.Network.Routing.Channel;
using Nalix.Network.Routing.Options;
using Nalix.Shared.Extensions;
using System.Linq;

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
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("Running={_running}, Pending={_dispatch.TotalPackets}")]
public sealed class PacketDispatchChannel
    : PacketDispatcherBase<IPacket>, IPacketDispatch, System.IDisposable, IActivatable, IReportable
{
    #region Fields

    private readonly IPacketRegistry _catalog;
    private readonly DispatchChannel<IPacket> _dispatch;
    private readonly System.Threading.SemaphoreSlim _semaphore = new(0);
    private readonly System.Threading.CancellationTokenSource _cts = new();

    private System.Int32 _running;
    private System.Int32 _activeLoops;
    private System.Int32 _dispatchLoops;
    private IWorkerHandle[] _workerHandle;
    private System.Threading.CancellationTokenSource _linkedCts;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchChannel"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketDispatchChannel(System.Action<PacketDispatchOptions<IPacket>> options) : base(options)
    {
        _dispatch = new DispatchChannel<IPacket>();
        _catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
                   ?? throw new System.InvalidOperationException(
                       $"[{nameof(PacketDispatchChannel)}] IPacketRegistry not registered in InstanceManager. Make sure to build and register IPacketRegistry before starting dispatcher.");

        // Push any additional initialization here if needed
        Logging?.Debug($"[{nameof(PacketDispatchChannel)}] init");
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Starts the lease processing loop
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Activate(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            Logging?.Debug($"[{nameof(PacketDispatchChannel)}:{Activate}] already-running");
            return;
        }

        System.Threading.CancellationToken linkedToken;

        if (cancellationToken.CanBeCanceled)
        {
            _linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
            linkedToken = _linkedCts.Token;
        }
        else
        {
            linkedToken = _cts.Token;
        }

        System.Threading.Volatile.Write(ref _activeLoops, 0);

        // Decide how many parallel dispatch loops to start.
        // Rule of thumb: cores/2, clamped to [1..12]
        _dispatchLoops = System.Math.Clamp(System.Environment.ProcessorCount / 2, 1, 12);
        _workerHandle = new IWorkerHandle[_dispatchLoops];

        for (System.Int32 i = 0; i < _dispatchLoops; i++)
        {
            System.Threading.Interlocked.Increment(ref _activeLoops);

            _workerHandle[i] = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"{TaskNaming.Tags.Dispatch}.{TaskNaming.Tags.Process}.{i}",
                group: $"{NetTaskNames.Net}/{TaskNaming.Tags.Dispatch}",
                work: async (ctx, ct) => await RunLoop(ctx, ct).ConfigureAwait(false),
                options: new WorkerOptions
                {
                    IdType = SnowflakeType.System,
                    CancellationToken = linkedToken,
                    RetainFor = System.TimeSpan.Zero,
                    Tag = NetTaskNames.Net
                }
            );
        }

        Logging?.Trace($"[{nameof(PacketDispatchChannel)}:{Activate}] start");
    }

    /// <summary>
    /// Stops the lease processing loop
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Deactivate(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Interlocked.Exchange(ref _running, 0) == 0)
        {
            return;
        }

        try
        {
            for (System.Int32 i = 0; i < _dispatchLoops; i++)
            {
                System.Threading.Interlocked.Decrement(ref _activeLoops);
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_workerHandle[i].Id);
            }

            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Logging?.Trace($"[{nameof(PacketDispatchChannel)}:{Deactivate}] stop");
            }

            try
            {
                System.Int32 releases = System.Math.Max(_dispatchLoops, 1);
                for (System.Int32 i = 0; i < releases; i++)
                {
                    _ = _semaphore.Release();
                }
            }
            catch { /* ignore over-release */ }
        }
        catch (System.ObjectDisposedException)
        {
            Logging?.Warn($"[{nameof(PacketDispatchChannel)}:{Deactivate}] stop-on-disposed-cts");
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[{nameof(PacketDispatchChannel)}:{Deactivate}] stop-error", ex);
        }
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(
        [System.Diagnostics.CodeAnalysis.MaybeNull] IBufferLease lease,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
    {
        if (lease is null || lease.Length <= 0)
        {
            Logging?.Debug($"[{nameof(PacketDispatchChannel)}:{nameof(HandlePacket)}] empty-payload ep={connection.NetworkEndpoint}");
            lease?.Dispose();

            return;
        }

        // Enqueue lease into the priority-aware channel (per-connection).
        _dispatch.Push(connection, lease);

        // Signal the worker that an item is available.
        _ = _semaphore.Release();
    }

    /// <inheritdoc />
    // If you want typed fast-path, you can implement a separate typed channel.
    // For now, process immediately to avoid mixing typed/lease queues.
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void HandlePacket(
        [System.Diagnostics.CodeAnalysis.NotNull] IPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection) => base.ExecutePacketHandlerAsync(packet, connection).Await();

    #endregion Public Methods

    #region IReportable

    /// <summary>
    /// Generates a human-readable diagnostic report for the PacketDispatchChannel.
    /// </summary>
    /// <returns>A formatted report string.</returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.String GenerateReport()
    {
        var sb = new System.Text.StringBuilder(2048);

        // Header
        sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PacketDispatchChannel:");
        sb.AppendLine($"Running           : {(System.Threading.Volatile.Read(ref _running) == 1 ? "Yes" : "No")}");
        sb.AppendLine($"DispatchLoops     : {_dispatchLoops}");
        sb.AppendLine();

        sb.AppendLine("---------------------------------------------------------------------");
        sb.AppendLine("Channel Statistics:");
        sb.AppendLine($"Total Packets     : {_dispatch.TotalPackets}");
        sb.AppendLine($"Total Connections : {_dispatch.TotalConnections}");
        sb.AppendLine($"Ready Connections : {_dispatch.ReadyConnections}");
        sb.AppendLine();

        sb.AppendLine("Pending by Priority:");
        sb.AppendLine("Priority          | Pending Connections");
        sb.AppendLine("------------------|---------------------");
        var priorities = _dispatch.PendingPerPriority;
        for (System.Int32 p = priorities.Length - 1; p >= 0; p--)
        {
            sb.AppendLine($"{GetPriorityName(p),-18}| {priorities[p],-19}");
        }

        sb.AppendLine();

        sb.AppendLine("Top Connections by Pending Packets:");
        sb.AppendLine("EndPoint              | Pending");
        sb.AppendLine("----------------------|----------");
        foreach (var kv in _dispatch.PendingPerConnection.OrderByDescending(x => x.Value).Take(10))
        {
            sb.AppendLine($"{kv.Key.NetworkEndpoint,-22}| {kv.Value,6}");
        }

        sb.AppendLine();

        sb.AppendLine("---------------------------------------------------------------------");
        sb.AppendLine("Resources / Metrics:");
        sb.AppendLine($"Semaphore.CurrentCount: {_semaphore.CurrentCount}");
        sb.AppendLine($"CTS.Cancelled         : {_cts.IsCancellationRequested}");
        sb.AppendLine();

        sb.AppendLine("---------------------------------------------------------------------");
        sb.AppendLine("Packet Registry:");
        sb.AppendLine($"Registry Type         : {_catalog?.GetType().Name ?? "(null)"}");
        sb.AppendLine();

        // Optionally list registered handlers if available in _catalog
        // sb.AppendLine("Registered handlers   : ...");

        sb.AppendLine("---------------------------------------------------------------------");
        sb.AppendLine("Quick Notes:");
        sb.AppendLine("• TotalPackets        = total packets pending for processing");
        sb.AppendLine("• ReadyConnections    = number of connections with packets ready for processing");
        sb.AppendLine("• PendingPerPriority  = number of ready connections by priority");
        sb.AppendLine("• Top Connections     = endpoints with the most pending packets");
        sb.AppendLine("• Resources/Memory    = system resource statistics");
        sb.AppendLine();

        return sb.ToString();
    }

    private static System.String GetPriorityName(System.Int32 index)
    {
        try
        {
            return System.Enum.GetName(typeof(PacketPriority), index) ?? index.ToString();
        }
        catch
        {
            return index.ToString();
        }
    }

    #endregion IReportable

    #region Private Methods

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private async System.Threading.Tasks.Task RunLoop(IWorkerContext ctx, System.Threading.CancellationToken ct)
    {
        System.TimeSpan heartbeatInterval = System.TimeSpan.FromSeconds(1);

        try
        {
            while (System.Threading.Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                ctx.Beat();

                System.Boolean signaled = await _semaphore.WaitAsync(heartbeatInterval, ct).ConfigureAwait(false);
                if (!signaled)
                {
                    continue;
                }

                // Pull from channel (priority-aware)
                if (!_dispatch.Pull(out IConnection connection, out IBufferLease lease))
                {
                    if (!_dispatch.HasPacket)
                    {
                        continue;
                    }

                    Logging?.Trace($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] pull-empty");
                    lease?.Dispose();
                    lease = null;
                    continue;
                }

                try
                {
                    // Xử lý middleware (thay đổi lease nếu cần)
                    IBufferLease afterMw = await Options.NetworkPipeline.ExecuteAsync(lease, connection, ct).ConfigureAwait(false);

                    if (afterMw is null)
                    {
                        Logging?.Debug($"[PacketDispatchChannel:RunLoop] middleware-reject ep={connection.NetworkEndpoint}");
                        lease.Dispose();
                        lease = null;
                        continue;
                    }

                    // Nếu middleware trả về lease mới, dispose lease cũ
                    if (afterMw != lease)
                    {
                        lease.Dispose();
                    }
                    lease = afterMw;
                }
                catch (System.Exception ex)
                {
                    connection.IncrementErrorCount();
                    Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] buffer-middleware-error ep={connection.NetworkEndpoint} leaseLen={lease?.Length}", ex);

                    lease?.Dispose();
                    lease = null;
                    continue;
                }

                try
                {
                    // Deserialize packet
                    if (!_catalog.TryDeserialize(lease.Span, out IPacket packet) || packet is null)
                    {
                        System.Int32 len = lease.Length;
                        System.String head = System.Convert.ToHexString(lease.Span[..System.Math.Min(16, len)]);
                        Logging?.Warn($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] deserialize-none ep={connection.NetworkEndpoint} len={len} head={head}");

                        lease.Dispose();
                        lease = null;
                        continue;
                    }

                    await base.ExecutePacketHandlerAsync(packet, connection).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    connection.IncrementErrorCount();
                    Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] handle-error ep={connection.NetworkEndpoint}", ex);
                }
                finally
                {
                    lease?.Dispose();
                    lease = null;
                }

                ctx.Advance(1);
            }
        }
        catch (System.OperationCanceledException)
        {
            // NONE cancellation, no need to log
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] loop-error", ex);
        }
        finally
        {
            if (System.Threading.Interlocked.Decrement(ref _activeLoops) == 0)
            {
                System.Threading.Volatile.Write(ref _running, 0);
            }
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        this.Deactivate();

        _linkedCts?.Dispose();

        this._cts.Dispose();
        this._semaphore.Dispose();
    }

    #endregion IDisposable
}
