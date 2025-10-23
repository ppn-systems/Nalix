// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Tasks;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch.Channel;
using Nalix.Network.Internal.Net;

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
/// It is suitable for high-throughput systems such as custom Reliable servers, IoT message brokers, or game servers
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
[System.Diagnostics.DebuggerDisplay("Running={_running}, Pending={_dispatch.TotalPackets}")]
public sealed class PacketDispatchChannel
    : PacketDispatcherBase<IPacket>, IPacketDispatch<IPacket>, System.IDisposable, IActivatable
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
    public PacketDispatchChannel(System.Action<Options.PacketDispatchOptions<IPacket>> options)
        : base(options)
    {
        _dispatch = new DispatchChannel<IPacket>(InstanceManager.Instance.GetExistingInstance<ILogger>());
        _catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                   ?? throw new System.InvalidOperationException(
                       $"[{nameof(PacketDispatchChannel)}] IPacketCatalog not registered in InstanceManager. " +
                       "Make sure to build and register IPacketCatalog before starting dispatcher.");

        // Push any additional initialization here if needed
        Logger?.Debug($"[{nameof(PacketDispatchChannel)}] init");
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Starts the lease processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Activate(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            Logger?.Debug($"[{nameof(PacketDispatchChannel)}:{Activate}] already-running");
            return;
        }

        System.Threading.CancellationToken linkedToken = cancellationToken.CanBeCanceled
                ? System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token).Token
                : _cts.Token;

        // Decide how many parallel dispatch loops to start.
        // Rule of thumb: cores/2, clamped to [2..12]
        System.Int32 cores = System.Environment.ProcessorCount;
        _dispatchLoops = System.Math.Clamp(cores / 2, 2, 12);

        for (System.Int32 i = 0; i < _dispatchLoops; i++)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
                name: $"{NetTaskCatalog.PacketDispatchWorker}_{i}",
                group: NetTaskCatalog.PacketDispatchGroup,
                work: async (ctx, ct) => await RunLoop(ctx, ct).ConfigureAwait(false),
                options: new WorkerOptions
                {
                    CancellationToken = linkedToken,
                    RetainFor = System.TimeSpan.Zero,
                    Tag = TaskNames.Tags.Dispatch
                });
        }

        Logger?.Trace($"[{nameof(PacketDispatchChannel)}:{Activate}] start");
    }

    /// <summary>
    /// Stops the lease processing loop
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Interlocked.Exchange(ref _running, 0) == 0)
        {
            return;
        }

        try
        {
            if (!this._cts.IsCancellationRequested)
            {
                this._cts.Cancel();
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
    public void HandlePacket(IBufferLease? lease, IConnection connection)
    {
        if (lease is null || lease.Length <= 0)
        {
            Logger?.Warn($"[{nameof(PacketDispatchChannel)}:{nameof(HandlePacket)}] empty-payload ep={connection.RemoteEndPoint}");
            lease?.Dispose();

            return;
        }

        // Enqueue lease into the priority-aware channel (per-connection).
        _dispatch.Push(connection, lease);

        // Signal the worker that an item is available.
        _ = _semaphore.Release();
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IPacket packet, IConnection connection)
    {
        // If you want typed fast-path, you can implement a separate typed channel.
        // For now, process immediately to avoid mixing typed/lease queues.
        _ = base.ExecutePacketHandlerAsync(packet, connection);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    private async System.Threading.Tasks.Task RunLoop(
        IWorkerContext ctx,
        System.Threading.CancellationToken ct)
    {
        try
        {
            while (System.Threading.Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                // Wait for packets to be available
                await _semaphore.WaitAsync(_cts.Token)
                                .ConfigureAwait(false);

                // Pull from channel (priority-aware)
                if (!_dispatch.Pull(out IConnection connection, out IBufferLease? lease))
                {
                    // Rare: signaled but nothing pulled (remove/drain race)
                    Logger?.Trace($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] pull-empty");
                    lease?.Dispose();

                    continue;
                }

                // Deserialize late (zero-alloc header reads were already done in the channel)
                try
                {
                    if (!_catalog.TryDeserialize(lease.Span, out IPacket? packet) || packet is null)
                    {
                        // Warn with small head preview
                        System.Int32 len = lease.Length;
                        System.String head = System.Convert.ToHexString(lease.Span[..System.Math.Min(16, len)]);
                        Logger?.Warn($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] deserialize-none ep={connection.RemoteEndPoint} len={len} head={head}");
                        continue;
                    }

                    await base.ExecutePacketHandlerAsync(packet, connection).ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    Logger?.Error($"[{nameof(PacketDispatchChannel)}:{nameof(RunLoop)}] handle-error ep={connection.RemoteEndPoint}", ex);
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
            // None cancellation, no need to log
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