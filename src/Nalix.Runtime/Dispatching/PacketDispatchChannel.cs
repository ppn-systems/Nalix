// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Routing;
using Nalix.Runtime.Internal.Compilation;
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

    private readonly DispatchChannel<IPacket> _dispatch;

    private readonly SemaphoreSlim _wakeSignal;
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
        _dispatch = new DispatchChannel<IPacket>();
        _wakeSignal = new SemaphoreSlim(0, int.MaxValue);
        _maxDrainPerWake = Math.Clamp(System.Environment.ProcessorCount * this.Options.MaxDrainPerWakeMultiplier, this.Options.MinDrainPerWake, this.Options.MaxDrainPerWake);
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

        Volatile.Write(ref _activeLoops, 0);

        _dispatchLoops = this.Options.DispatchLoopCount ?? Math.Clamp(System.Environment.ProcessorCount, this.Options.MinDispatchLoops, this.Options.MaxDispatchLoops);
        _workerHandle = new IWorkerHandle[_dispatchLoops];
        CancellationToken linkedTokenRef = linkedToken;

        for (int i = 0; i < _dispatchLoops; i++)
        {
            _ = Interlocked.Increment(ref _activeLoops);

            int loopIndex = i;
            _workerHandle[i] = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"{TaskNaming.Tags.Dispatch}.{TaskNaming.Tags.Process}.{i}",
                group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Dispatch}",
                work: async (ctx, ct) =>
                {
                    // This is now a truly asynchronous worker managed by TaskManager.
                    // By removing the manual OS thread, we reduce context switching and 
                    // allow .NET's thread pool to optimize the execution of the async state machine.
                    await this.DispatchWorkerLoopAsync(ctx, ct, loopIndex).ConfigureAwait(false);
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
            _ = _wakeSignal.Release(wakeCount);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Error))
            {
                this.Logging.LogError(ex, $"[RT.{nameof(PacketDispatchChannel)}:{nameof(Deactivate)}] deactivate-error");
            }
        }
        finally
        {
            try
            {
                linkedCts?.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Warning))
                {
                    this.Logging.LogWarning(ex, $"[RT.{nameof(PacketDispatchChannel)}:{nameof(Deactivate)}] linked-cts-dispose-failed");
                }
            }

            try
            {
                localCts?.Dispose();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Warning))
                {
                    this.Logging.LogWarning(ex, $"[RT.{nameof(PacketDispatchChannel)}:{nameof(Deactivate)}] local-cts-dispose-failed");
                }
            }
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IBufferLease lease, IConnection connection)
    {
        if (lease is null || connection is null)
        {
            lease?.Dispose();
            return;
        }

        if ((uint)lease.Length < PacketConstants.HeaderSize)
        {
            lease.Dispose();
            connection.IncrementErrorCount();
            return;
        }

        /*
         * [Asynchronous Handoff & Reference Management]
         * The transport layer owns the buffer until this call returns. 
         * To safely process the packet asynchronously in a worker thread, 
         * we MUST take an additional reference (Retain).
         */
        lease.Retain();

        if (!_dispatch.PushCore(connection, lease, noBlock: true))
        {
            // If the channel is full or the connection is inactive, we must
            // release the reference we just took to avoid a memory leak.
            lease.Dispose();
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

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteBoolean("Running", Volatile.Read(ref _running) == 1);
        writer.WriteNumber("DispatchLoops", _dispatchLoops);
        writer.WriteNumber("TotalPackets", _dispatch.TotalPackets);
        writer.WriteNumber("TotalConnections", _dispatch.TotalConnections);
        writer.WriteNumber("ReadyConnections", _dispatch.ReadyConnections);
        writer.WriteNumber("WakeSignals", Interlocked.Read(ref _wakeSignals));
        writer.WriteNumber("WakeReads", Interlocked.Read(ref _wakeReadSignals));
        writer.WriteBoolean("WakeRequested", Volatile.Read(ref _wakeRequested) == 1);
        writer.WriteString("PacketRegistryType", nameof(PacketRegistry));

        int[] priorities = _dispatch.PendingPerPriority;
        writer.WriteStartObject("PendingPerPriority");
        for (int p = priorities.Length - 1; p >= 0; p--)
        {
            writer.WriteNumber(GetPriorityName(p), priorities[p]);
        }
        writer.WriteEndObject();

        List<KeyValuePair<IConnection, int>> ranked = [.. _dispatch.PendingPerConnection];
        ranked.Sort(static (a, b) => b.Value.CompareTo(a.Value));

        int top = Math.Min(10, ranked.Count);
        writer.WriteStartArray("PendingByConnection");
        for (int i = 0; i < top; i++)
        {
            KeyValuePair<IConnection, int> kv = ranked[i];
            writer.WriteStartObject();
            writer.WriteString("EndPoint", kv.Key.NetworkEndpoint.Address);
            writer.WriteNumber("Pending", kv.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
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
    private async ValueTask DispatchWorkerLoopAsync(IWorkerContext ctx, CancellationToken ct, int index)
    {
        // PURE ASYNC LOOP.
        // Performance is maintained by pooled async state machines in .NET 10.
        // Eliminates Gen 1 GC pressure by removing '.AsTask()' allocations.

        try
        {
            // Loop while work is available, with occasional yields to TaskManager.
            while (Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                int processed = 0;

#pragma warning disable CA2000
                /*
                 * [The Hot Loop: Draining the Channel]
                 * We drain up to _maxDrainPerWake packets in a single pass.
                 * Draining is asynchronous but optimized: synchronous completions 
                 * (Abstractions) are essentially free.
                 */
                while (processed < _maxDrainPerWake &&
                       _dispatch.Pull(out IConnection? connection, out IBufferLease? lease))
                {
                    // Dispatch directly using await. 
                    // Zero-allocation for sync completion; Pooled for async.
                    await this.ExecutePacketAsync(connection, lease, ct).ConfigureAwait(false);
                    processed++;
                }
#pragma warning restore CA2000

                if (processed > 0)
                {
                    ctx.Advance(processed);
                    // If we processed some but were capped by _maxDrainPerWake,
                    // continue immediately to finish draining without waiting for signal.
                    if (processed >= _maxDrainPerWake)
                    {
                        continue;
                    }
                }

                // No more packets immediately available.
                // Reset wake requested flag before waiting.
                _ = Interlocked.Exchange(ref _wakeRequested, 0);

                // Check again before waiting to avoid lost wake-up.
                if (_dispatch.TotalPackets > 0)
                {
                    continue;
                }

                try
                {
                    /*
                     * [Wait Strategy: Coalesced Wake-up]
                     * If the channel is empty, we enter an asynchronous wait.
                     * We use a SemaphoreSlim to wake up just enough workers 
                     * based on the incoming load.
                     */
                    int spins = 0;
                    while (_dispatch.TotalPackets == 0 && spins < 16)
                    {
                        Thread.SpinWait(8);
                        spins++;
                    }

                    if (_dispatch.TotalPackets > 0)
                    {
                        continue;
                    }

                    // Zero-allocation asynchronous wait.
                    _ = await _wakeSignal.WaitAsync(millisecondsTimeout: 50)
                                         .ConfigureAwait(false);

                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                ctx.Beat();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Error))
            {
                this.Logging.LogError(ex, $"[RT.{nameof(PacketDispatchChannel)}:DispatchWorkerLoopAsync] fatal-loop-error index={index}");
            }
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
    private ValueTask ExecutePacketAsync(IConnection connection, IBufferLease lease, CancellationToken ct)
    {
        // 1. Read the packet header directly from the raw span to determine routing
        PacketHeader header = MemoryMarshal.Read<PacketHeader>(lease.Span);

        // 2. Resolve the handler using the parsed opcode
        if (!this.Options.TryResolveHandler(header.OpCode, out PacketHandler<IPacket> handler))
        {
            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Warning))
            {
                connection.ThrottledWarn(
                    this.Logging,
                    "dispatch.execute",
                    $"[RT.{nameof(PacketDispatchChannel)}:{nameof(ExecutePacketAsync)}] no-handler opcode={header.OpCode}");
            }

            lease.Dispose();
            connection.IncrementErrorCount();
            return ValueTask.CompletedTask;
        }

        IPacket packet;

        // 3. Bypass deserialization if the handler expects raw memory
        if (handler.ExpectedPacketType == typeof(MemoryPacket))
        {
            packet = new MemoryPacket(lease.Memory, header);
        }
        else
        {
            // 4. Normal deserialization fallback for structured packets
            if (!PacketRegistry.TryDeserialize(lease.Span, out IPacket? deserialized) || deserialized is null)
            {
                lease.Dispose();
                connection.IncrementErrorCount();
                return ValueTask.CompletedTask;
            }

            packet = deserialized;
        }

        try
        {
            /*
             * [Packet Handler Execution]
             * 1. Attempt to execute the resolved handler.
             * 2. If it completes synchronously, we can dispose resources immediately.
             * 3. If it's asynchronous, we hand off to AwaitPacketHandlerCompletionAsync.
             */
            ValueTask pending = this.Options.ExecuteResolvedHandlerAsync(in handler, packet, connection, ct);

            // Fast-path: handler completed synchronously
            if (pending.IsCompletedSuccessfully)
            {
                if (packet is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                lease.Dispose();
                return ValueTask.CompletedTask;
            }

            // Slow-path: async completion (AwaitDispatchAsync handles Return/Dispose)
            return AwaitPacketHandlerCompletionAsync(this, connection, lease, packet, pending, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // External cancellation during sync execution
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            connection.IncrementErrorCount();

            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Error))
            {
                connection.ThrottledError(
                    this.Logging,
                    "dispatch.execute",
                    $"[RT.{nameof(PacketDispatchChannel)}:{nameof(ExecutePacketAsync)}] handler-error ep={connection.NetworkEndpoint}");
            }
        }

        // 5. Cleanup for synchronous errors/cancellation
        if (packet is IDisposable disposableSync)
        {
            disposableSync.Dispose();
        }

        lease.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async ValueTask AwaitPacketHandlerCompletionAsync(
        PacketDispatchChannel owner, IConnection connection,
        IBufferLease lease, IPacket packet, ValueTask pending, CancellationToken ct)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Async cancellation
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            connection.IncrementErrorCount();
            if (owner.Logging != null && owner.Logging.IsEnabled(LogLevel.Error))
            {
                connection.ThrottledError(
                    owner.Logging,
                    "dispatch.execute",
                    $"[RT.{nameof(PacketDispatchChannel)}:{nameof(ExecutePacketAsync)}] handler-error ep={connection.NetworkEndpoint}");
            }
        }
        finally
        {
            // Guaranteed release for async path
            if (packet is IDisposable disposable)
            {
                disposable.Dispose();
            }

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

        // Calculate how many wake signals to release.
        // Wake less frequently when the queue is not full.
        // If there are too many packets, wake all dispatch loops. 
        // Otherwise, wake just enough based on the number of packets.
        long total = _dispatch.TotalPackets;
        int count = total > _maxDrainPerWake ? _dispatchLoops : Math.Max(1, (int)(total / _maxDrainPerWake * _dispatchLoops) + 1);

        _ = _wakeSignal.Release(count);
        _ = Interlocked.Increment(ref _wakeSignals);
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
        _dispatch.Dispose();
        _wakeSignal.Dispose();
        _linkedCts?.Dispose();
        _cts?.Dispose();
    }

    #endregion IDisposable
}
