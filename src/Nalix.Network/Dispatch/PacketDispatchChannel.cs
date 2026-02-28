// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Connection;
using Nalix.Common.Enums;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch.Channel;
using Nalix.Network.Internal;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch;

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

    private readonly IPacketCatalog _catalog;
    private readonly DispatchChannel<IPacket> _dispatch;
    private readonly System.Threading.SemaphoreSlim _semaphore = new(0);
    private readonly System.Threading.CancellationTokenSource _cts = new();

    private System.Int32 _running;
    private System.Int32 _dispatchLoops;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchChannel"/> class
    /// with custom configuration options.
    /// </summary>
    /// <param name="options">A delegate used to configure dispatcher options</param>
    public PacketDispatchChannel(System.Action<Options.PacketDispatchOptions<IPacket>> options) : base(options)
    {
        _dispatch = new DispatchChannel<IPacket>();
        _catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                   ?? throw new System.InvalidOperationException(
                       $"[{nameof(PacketDispatchChannel)}] IPacketCatalog not registered in InstanceManager. Make sure to build and register IPacketCatalog before starting dispatcher.");

        // Push any additional initialization here if needed
        Logger?.Debug($"[{nameof(PacketDispatchChannel)}] init");
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
            Logger?.Debug($"[{nameof(PacketDispatchChannel)}:{Activate}] already-running");
            return;
        }

        System.Threading.CancellationToken linkedToken = cancellationToken.CanBeCanceled
            ? System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token).Token : _cts.Token;

        // Decide how many parallel dispatch loops to start.
        // Rule of thumb: cores/2, clamped to [1..12]
        _dispatchLoops = System.Math.Clamp(System.Environment.ProcessorCount / 2, 1, 12);

        for (System.Int32 i = 0; i < _dispatchLoops; i++)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"{NetTaskNames.PacketDispatchWorker()}_{i}",
                group: NetTaskNames.PacketDispatchGroup,
                work: async (ctx, ct) => await RunLoop(ctx, ct).ConfigureAwait(false),
                options: new WorkerOptions
                {
                    IdType = SnowflakeType.System,
                    CancellationToken = linkedToken,
                    RetainFor = System.TimeSpan.Zero,
                    Tag = TaskNaming.Tags.Dispatch
                });
        }

        Logger?.Trace($"[{nameof(PacketDispatchChannel)}:{Activate}] start");
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
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Logger?.Trace($"[{nameof(PacketDispatchChannel)}:{Deactivate}] stop");
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
            Logger?.Warn($"[{nameof(PacketDispatchChannel)}:{Deactivate}] stop-on-disposed-cts");
        }
        catch (System.Exception ex)
        {
            Logger?.Error($"[{nameof(PacketDispatchChannel)}:{Deactivate}] stop-error", ex);
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
            Logger?.Warn($"[{nameof(PacketDispatchChannel)}:{nameof(HandlePacket)}] empty-payload ep={connection.EndPoint}");
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
        try
        {
            while (System.Threading.Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                // Wait for packets to be available
                await _semaphore.WaitAsync(ct)
                                .ConfigureAwait(false);

                // Pull from channel (priority-aware)
                if (!_dispatch.Pull(out IConnection connection, out IBufferLease lease))
                {
                    // Semaphore was signaled but packets already drained by other workers
                    if (!_dispatch.HasPacket)
                    {
                        // No packets left, just continue waiting
                        continue;
                    }

                    // Rare: signaled but nothing pulled (remove/drain race)
                    Logger?.Trace($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] pull-empty");
                    lease?.Dispose();

                    continue;
                }

                // Deserialize late (zero-alloc header reads were already done in the channel)
                try
                {
                    if (!_catalog.TryDeserialize(lease.Span, out IPacket packet) || packet is null)
                    {
                        // Warn with small head preview
                        System.Int32 len = lease.Length;
                        System.String head = System.Convert.ToHexString(lease.Span[..System.Math.Min(16, len)]);
                        Logger?.Warn($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] deserialize-none ep={connection.EndPoint} len={len} head={head}");
                        continue;
                    }

                    await base.ExecutePacketHandlerAsync(packet, connection).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    Logger?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] handle-error ep={connection.EndPoint}", ex);
                }
                finally
                {
                    lease?.Dispose();
                }

                ctx.Advance(1);
                ctx.Beat();
            }
        }
        catch (System.OperationCanceledException)
        {
            // NONE cancellation, no need to log
        }
        catch (System.Exception ex)
        {
            Logger?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] loop-error", ex);
        }
        finally
        {
            System.Threading.Volatile.Write(ref _running, 0);
        }
    }

    #endregion Private Methods

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
        System.Text.StringBuilder sb = new(1024);

        APPEND_HEADER(sb);
        APPEND_SEMAPHORE_AND_CTS_INFO(sb);
        APPEND_DISPATCH_DIAGNOSTICS(sb);
        APPEND_CATALOG_INFO(sb);
        APPEND_NOTES(sb);

        return sb.ToString();
    }

    /* Helper methods - keep English comments and XML docs per project standard. */

    private void APPEND_HEADER(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PacketDispatchChannel:");
        _ = sb.AppendLine($"Running: {(System.Threading.Volatile.Read(ref _running) == 1 ? "yes" : " no")} | DispatchLoops: {_dispatchLoops} | PendingPackets: {_dispatch?.TotalPackets ?? 0}");
        _ = sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");
    }

    private void APPEND_SEMAPHORE_AND_CTS_INFO(System.Text.StringBuilder sb)
    {
        try
        {
            _ = sb.AppendLine($"Semaphore.CurrentCount: {_semaphore.CurrentCount} | CTS.Cancelled: {_cts.IsCancellationRequested}");
        }
        catch (System.Exception)
        {
            // Best-effort: do not throw from diagnostics.
            _ = sb.AppendLine($"Semaphore.CurrentCount: - | CTS.Cancelled: {_cts.IsCancellationRequested}");
        }

        _ = sb.AppendLine();
    }

    private void APPEND_DISPATCH_DIAGNOSTICS(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine("DispatchChannel diagnostics (best-effort via reflection):");

        System.Object dispatchObj = _dispatch;
        if (dispatchObj == null)
        {
            _ = sb.AppendLine("  DispatchChannel: null");
            _ = sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");
            return;
        }

        System.Type dType = dispatchObj.GetType();

        // Ready queues per priority
        try
        {
            System.Reflection.FieldInfo readyField = dType.GetField("_readyByPrio", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (readyField?.GetValue(dispatchObj) is System.Array readyVal)
            {
                _ = sb.AppendLine("  Ready queues (per-priority) - approximate queued connections:");
                for (System.Int32 p = readyVal.Length - 1; p >= 0; p--)
                {
                    System.Int32 count = TRY_GET_COLLECTION_COUNT(readyVal.GetValue(p));
                    System.String prioName = GET_PRIORITY_NAME(p);
                    _ = sb.AppendLine($"    {prioName,-8} : {count,6}");
                }
            }
            else
            {
                _ = sb.AppendLine("  Ready queues: -");
            }
        }
        catch (System.Exception ex)
        {
            _ = sb.AppendLine($"  Ready queues: reflection error: {ex.GetType().Name} {ex.Message}");
        }

        // inReady set count
        try
        {
            System.Int32 inReadyCount = TRY_GET_PRIVATE_COLLECTION_COUNT(dispatchObj, "_inReady");
            _ = sb.AppendLine($"  Connections enqueued (inReady): {inReadyCount}");
        }
        catch (System.Exception ex)
        {
            _ = sb.AppendLine($"  inReady: reflection error: {ex.GetType().Name} {ex.Message}");
        }

        // _states count
        try
        {
            System.Int32 statesCount = TRY_GET_PRIVATE_COLLECTION_COUNT(dispatchObj, "_states");
            _ = sb.AppendLine($"  Connections tracked (_states): {statesCount}");
        }
        catch (System.Exception ex)
        {
            _ = sb.AppendLine($"  _states: reflection error: {ex.GetType().Name} {ex.Message}");
        }

        // _queues count
        try
        {
            System.Int32 queuesCount = TRY_GET_PRIVATE_COLLECTION_COUNT(dispatchObj, "_queues");
            _ = sb.AppendLine($"  Connections with queues (_queues): {queuesCount}");
        }
        catch (System.Exception ex)
        {
            _ = sb.AppendLine($"  _queues: reflection error: {ex.GetType().Name} {ex.Message}");
        }

        // _totalPackets (private int)
        try
        {
            System.Int32 totalPackets = TRY_GET_PRIVATE_INT_FIELD(dispatchObj, "_totalPackets");
            if (totalPackets >= 0)
            {
                _ = sb.AppendLine($"  _totalPackets (private): {totalPackets}");
            }
        }
        catch (System.Exception ex)
        {
            _ = sb.AppendLine($"  _totalPackets: reflection error: {ex.GetType().Name} {ex.Message}");
        }

        _ = sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");
    }

    /// <summary>
    /// Returns the priority name for a priority index if the PacketPriority enum is available; otherwise returns the numeric index.
    /// </summary>
    private static System.String GET_PRIORITY_NAME(System.Int32 index)
    {
        try
        {
            var enumType = typeof(PacketPriority);
            System.String name = System.Enum.GetName(enumType, index);
            return name ?? index.ToString();
        }
        catch
        {
            return index.ToString();
        }
    }

    /// <summary>
    /// Tries to read an integer private field from an object. Returns -1 on failure.
    /// </summary>
    private static System.Int32 TRY_GET_PRIVATE_INT_FIELD(System.Object instance, System.String fieldName)
    {
        if (instance == null)
        {
            return -1;
        }

        var t = instance.GetType();
        var f = t.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null)
        {
            return -1;
        }

        try
        {
            var val = f.GetValue(instance);
            if (val is System.Int32 i)
            {
                return i;
            }

            if (val is System.Int64 l)
            {
                return (System.Int32)l;
            }

            if (val is System.Int32 i32)
            {
                return i32;
            }
        }
        catch { }
        return -1;
    }

    /// <summary>
    /// Tries to read a private collection-like field and return its Count (best-effort). Returns -1 on failure.
    /// </summary>
    private static System.Int32 TRY_GET_PRIVATE_COLLECTION_COUNT(System.Object instance, System.String fieldName)
    {
        if (instance == null)
        {
            return -1;
        }

        var t = instance.GetType();
        var f = t.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null)
        {
            return -1;
        }

        try
        {
            var val = f.GetValue(instance);
            return TRY_GET_COLLECTION_COUNT(val);
        }
        catch { }
        return -1;
    }

    /// <summary>
    /// Returns the Count of a collection-like object or -1 if it cannot be determined.
    /// </summary>
    private static System.Int32 TRY_GET_COLLECTION_COUNT(System.Object obj)
    {
        if (obj == null)
        {
            return -1;
        }

        // If it implements ICollection, use Count
        if (obj is System.Collections.ICollection coll)
        {
            try { return coll.Count; } catch { return -1; }
        }

        // If it has a Count property, try to read it via reflection
        try
        {
            var prop = obj.GetType().GetProperty("Count", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is System.Int32 i)
                {
                    return i;
                }

                if (val is System.Int64 l)
                {
                    return (System.Int32)l;
                }
            }
        }
        catch { }

        return -1;
    }

    private void APPEND_CATALOG_INFO(System.Text.StringBuilder sb)
    {
        try
        {
            System.String catalogType = _catalog?.GetType().FullName ?? "-";
            _ = sb.AppendLine($"PacketCatalog: {catalogType}");
        }
        catch
        {
            _ = sb.AppendLine("PacketCatalog: -");
        }

        _ = sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");
    }

    private static void APPEND_NOTES(System.Text.StringBuilder sb)
    {
        _ = sb.AppendLine("Notes:");
        _ = sb.AppendLine(" - semaphore = semaphore (synchronization counter)");
        _ = sb.AppendLine(" - CTS = CancellationTokenSource");
        _ = sb.AppendLine(" - pending packets = packets waiting inside dispatch channel");
        _ = sb.AppendLine(" - Reflection reads are best-effort; consider exposing diagnostic APIs on DispatchChannel for stable metrics.");
        _ = sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");
    }

    #endregion IReportable

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        this.Deactivate();
        this._cts.Dispose();
        this._semaphore.Dispose();
    }

    #endregion IDisposable
}