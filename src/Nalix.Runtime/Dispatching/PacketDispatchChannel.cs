// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
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
using Nalix.Environment.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Routing;
using Nalix.Runtime.Internal.Compilation;
using Nalix.Runtime.Internal.Routing;
using Nalix.Runtime.Middleware;

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
    private long _idleWorkers;
    private long _deserializationErrors;

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
        _maxDrainPerWake = Math.Clamp(System.Environment.ProcessorCount * this.Options.Drain.MaxDrainPerWakeMultiplier, this.Options.Drain.MinDrainPerWake, this.Options.Drain.MaxDrainPerWake);
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

        _ = Interlocked.Exchange(ref _idleWorkers, 0);
        _ = Interlocked.Exchange(ref _wakeSignals, 0);
        _ = Interlocked.Exchange(ref _wakeReadSignals, 0);

        Volatile.Write(ref _activeLoops, 0);

        _dispatchLoops = this.Options.Drain.Count == 0 ? Math.Clamp(System.Environment.ProcessorCount, this.Options.Drain.MinDispatchLoops, this.Options.Drain.MaxDispatchLoops) : this.Options.Drain.Count;
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
                    _workerHandle[i].Dispose();
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
                this.Logging.LogError(ex, "[RT.PacketDispatchChannel:Deactivate] deactivate-error");
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
                    this.Logging.LogWarning(ex, "[RT.PacketDispatchChannel:Deactivate] linked-cts-dispose-failed");
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
                    this.Logging.LogWarning(ex, "[RT.PacketDispatchChannel:Deactivate] local-cts-dispose-failed");
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

        if (connection.TCP.Framing == TransportFraming.UInt16LengthPrefixed && (uint)lease.Length < PacketConstants.HeaderSize)
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
        PipelineMetrics metrics = this.Options.Metrics;
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PacketDispatchChannel:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Running              : {(Volatile.Read(ref _running) == 1 ? "Yes" : "No")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"DispatchLoops        : {_dispatchLoops}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"WakeSignals          : {Interlocked.Read(ref _wakeSignals)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"WakeReads            : {Interlocked.Read(ref _wakeReadSignals)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"IdleWorkers          : {Volatile.Read(ref _idleWorkers)}");
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

        _ = sb.AppendLine();
        _ = sb.AppendLine("---------------------------------------------------------------------");
        _ = sb.AppendLine("Pipeline Statistics:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Active Executions    : {this.Options.Metrics.ActiveExecutions}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Executions     : {this.Options.Metrics.TotalExecutions}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Errors         : {this.Options.Metrics.TotalErrors}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Deserialization Err : {Volatile.Read(ref _deserializationErrors)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average Time (ms)    : {this.Options.Metrics.AverageExecutionTime.TotalMilliseconds:F4}");

        ReadOnlySpan<PerMiddlewareMetrics> mwMetrics = this.Options.MiddlewareMetrics;
        if (this.Options.MiddlewareMetrics.Length > 0)
        {
            _ = sb.AppendLine();
            _ = sb.AppendLine("Middleware Performance:");
            _ = sb.AppendLine("Middleware                           | Executions | Errors | Avg Time (ms)");
            _ = sb.AppendLine("-------------------------------------|------------|--------|--------------");
            foreach (ref readonly PerMiddlewareMetrics m in this.Options.MiddlewareMetrics)
            {
                double avgMs = m.TotalExecutions == 0 ? 0 : TimeSpan.FromTicks(m.TotalExecutionTicks / m.TotalExecutions).TotalMilliseconds;
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{m.MiddlewareType.Name,-36} | {m.TotalExecutions,-10} | {m.TotalErrors,-6} | {avgMs:F4}");
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        PipelineMetrics metrics = this.Options.Metrics;

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteBoolean("Running", Volatile.Read(ref _running) == 1);
        writer.WriteNumber("DispatchLoops", _dispatchLoops);
        writer.WriteNumber("TotalPackets", _dispatch.TotalPackets);
        writer.WriteNumber("TotalConnections", _dispatch.TotalConnections);
        writer.WriteNumber("ReadyConnections", _dispatch.ReadyConnections);
        writer.WriteNumber("WakeSignals", Interlocked.Read(ref _wakeSignals));
        writer.WriteNumber("WakeReads", Interlocked.Read(ref _wakeReadSignals));
        writer.WriteNumber("IdleWorkers", Volatile.Read(ref _idleWorkers));
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

        writer.WriteStartObject("PipelineMetrics");
        writer.WriteNumber("ActiveExecutions", this.Options.Metrics.ActiveExecutions);
        writer.WriteNumber("TotalExecutions", this.Options.Metrics.TotalExecutions);
        writer.WriteNumber("TotalErrors", this.Options.Metrics.TotalErrors);
        writer.WriteNumber("DeserializationErrors", Volatile.Read(ref _deserializationErrors));
        writer.WriteNumber("AverageTimeMs", this.Options.Metrics.AverageExecutionTime.TotalMilliseconds);
        writer.WriteEndObject();

        if (this.Options.MiddlewareMetrics.Length > 0)
        {
            writer.WriteStartArray("MiddlewareMetrics");
            foreach (ref readonly PerMiddlewareMetrics m in this.Options.MiddlewareMetrics)
            {
                writer.WriteStartObject();
                writer.WriteString("Type", m.MiddlewareType.Name);
                writer.WriteNumber("TotalExecutions", m.TotalExecutions);
                writer.WriteNumber("TotalErrors", m.TotalErrors);
                double avgMs = m.TotalExecutions == 0 ? 0 : TimeSpan.FromTicks(m.TotalExecutionTicks / m.TotalExecutions).TotalMilliseconds;
                writer.WriteNumber("AverageTimeMs", avgMs);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

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
        // WORK-STEALING / ACTOR-MAILBOX LOOP.
        // Each worker claims an entire connection's mailbox (session),
        // processes a batch of packets, then releases the claim.
        // This guarantees strict per-connection ordering while distributing
        // load evenly across all workers.

        try
        {
            // Loop while work is available, with occasional yields to TaskManager.
            while (Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                int processed = 0;

                if (_dispatch.TryClaim(out IDispatchSession? session))
                {
                    try
                    {
#pragma warning disable CA2000
                        /*
                         * [The Hot Loop: Draining a Claimed Connection]
                         * We drain up to _maxDrainPerWake packets from a single
                         * connection's mailbox. The session holds exclusive rights,
                         * so no other worker can touch this connection.
                         */
                        while (processed < _maxDrainPerWake &&
                               session.TryDequeue(out IBufferLease? lease))
                        {
                            // Dispatch directly using await.
                            // Zero-allocation for sync completion; Pooled for async.
                            await this.ExecutePacketAsync(session.Connection, lease, ct).ConfigureAwait(false);
                            processed++;
                        }
#pragma warning restore CA2000
                    }
                    finally
                    {
                        // Release the session: re-enqueues the connection if packets remain.
                        session.Dispose();
                    }
                }

                if (processed > 0)
                {
                    ctx.Advance(processed);

                    // If we hit the batch cap, immediately try to claim another session
                    // to keep draining without sleeping.
                    if (processed >= _maxDrainPerWake)
                    {
                        continue;
                    }
                }

                // Signal that this worker is about to go idle.
                _ = Interlocked.Increment(ref _idleWorkers);

                try
                {
                    /*
                     * [Wait Strategy: Idle Worker + Coalesced Wake-up]
                     * We register as idle before waiting so that the next
                     * RequestWake() will correctly Release() this worker's
                     * semaphore slot, eliminating the starvation bug.
                     */
                    int spins = 0;
                    while (_dispatch.TotalPackets == 0 && spins < 16)
                    {
                        Thread.SpinWait(8);
                        spins++;
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
                finally
                {
                    // Whether we were woken by signal or timeout, we are no longer idle.
                    // If we got work, we stay active; if not, we loop back and re-idle.
                    DecrementNonNegative(ref _idleWorkers);
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
                this.Logging.LogError(ex, "[RT.PacketDispatchChannel:DispatchWorkerLoopAsync] fatal-loop-error index={Index}", index);
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
        ushort opcode;
        IPacket packet;
        PacketHandler<IPacket> handler;

        try
        {
            // 1. Read the packet header directly from the raw span to determine routing
            opcode = connection.PacketClassifier.Extract(lease.Span);

            // 2. Resolve the handler using the parsed opcode
            if (!this.Options.TryResolveHandler(opcode, out handler))
            {
                if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Warning))
                {
                    connection.ThrottledWarn(
                        this.Logging,
                        "dispatch.execute",
                        $"[RT.{nameof(PacketDispatchChannel)}:{nameof(ExecutePacketAsync)}] no-handler opcode={opcode}");
                }

                lease.Dispose();
                connection.IncrementErrorCount();
                return ValueTask.CompletedTask;
            }

            // 3. Bypass deserialization if the handler expects raw memory
            if (handler.ExpectedPacketType == typeof(MemoryPacket))
            {
                // HACK: [Technical Debt] Initialize a "dummy" PacketHeader containing only OpCode
                // to satisfy the MemoryPacket constructor for routing.
                // WARNING: Other attributes such as SequenceId and Flags in this Header will have default values ​​(0).
                // Middleware (RateLimit, Timeout, Concurrency, etc.) must be cautious when reading data from Packet.Header.
                // TODO: In the future, if the system requires stricter QoS control, IOpCodeExtractor should be upgraded to IHeaderParser.

                if (lease.Length >= PacketConstants.HeaderSize)
                {
                    PacketHeader header = HeaderExtensions.AsHeaderRef(lease.Span);

                    header.OpCode = opcode;
                    packet = new MemoryPacket(lease.Memory, header);
                }
                else
                {
                    packet = new MemoryPacket(lease.Memory, new PacketHeader { OpCode = opcode });
                }
            }
            else
            {
                // 4. Normal deserialization fallback for structured packets
                if (!PacketRegistry.TryDeserialize(lease.Span, out IPacket? deserialized) || deserialized is null)
                {
                    _ = Interlocked.Increment(ref _deserializationErrors);
                    lease.Dispose();
                    connection.IncrementErrorCount();
                    return ValueTask.CompletedTask;
                }

                packet = deserialized;
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            connection.IncrementErrorCount();
            if (this.Logging != null && this.Logging.IsEnabled(LogLevel.Error))
            {
                connection.ThrottledError(
                    this.Logging,
                    "dispatch.execute_prep",
                    $"[RT.{nameof(PacketDispatchChannel)}:{nameof(ExecutePacketAsync)}] prepare-error ep={connection.NetworkEndpoint}", ex);
            }
            lease.Dispose();
            return ValueTask.CompletedTask;
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
        // Release exactly one wake signal for each currently idle worker.
        // This eliminates the starvation bug: under sustained load, the
        // semaphore is released every time a new packet arrives, waking
        // workers one-by-one until none remain idle.
        long idle = Volatile.Read(ref _idleWorkers);
        if (idle <= 0)
        {
            return;
        }

        int toWake = (int)Math.Min(idle, _dispatchLoops);
        if (toWake <= 0)
        {
            return;
        }

        _ = _wakeSignal.Release(toWake);
        _ = Interlocked.Increment(ref _wakeSignals);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecrementNonNegative(ref long value)
    {
        long after = Interlocked.Decrement(ref value);
        if (after < 0)
        {
            _ = Interlocked.Exchange(ref value, 0);
        }
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
