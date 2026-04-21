// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Options;

namespace Nalix.Network.Internal.Time;

/// <summary>
/// Provides an ultra-lightweight <b>Hashed Wheel Timer</b> for idle connection cleanup,
/// optimized to minimize allocations and avoid duplicate handlers.
/// </summary>
/// <remarks>
/// <para>
/// The timer periodically inspects registered <see cref="IConnection"/> instances and closes those
/// that have been idle longer than the configured threshold
/// (<see cref="TimingWheelOptions.IdleTimeoutMs"/>).
/// </para>
/// <para>
/// It uses a hashed timing wheel:
/// <list type="bullet">
///   <item>
///     <description>Each task is placed into a bucket computed from <c>tick % wheelSize</c>.</description>
///   </item>
///   <item>
///     <description><c>Rounds</c> indicates the number of full wheel rotations before execution.</description>
///   </item>
///   <item>
///     <description>On each tick, only the current bucket is processed for O(k) where k is items in that bucket.</description>
///   </item>
/// </list>
/// </para>
/// <para><b>Thread safety:</b> <see cref="Register(IConnection)"/> and <see cref="Unregister(IConnection)"/> are thread-safe.
/// The background loop is single-consumer and advances the wheel using <see cref="PeriodicTimer"/>.</para>
/// <para><b>Pool ownership:</b> <see cref="RUN_LOOP"/> is the <em>only</em> place that returns
/// <see cref="TimeoutTask"/> to the pool. <see cref="Unregister"/> only removes the connection from
/// <c>_active</c> — the task stays in the wheel queue until the loop dequeues it, at which point the
/// version mismatch causes a safe <c>Return</c>. This avoids the race where pool-reset clears
/// <c>task.Conn</c> before the loop has a chance to read it.</para>
/// </remarks>
/// <example>
/// <code>
/// var timer = new TimingWheel();
/// timer.Activate();
/// timer.Register(connection);
/// // ...
/// timer.Deactivate();
/// </code>
/// </example>
/// <seealso cref="IActivatable"/>
[SkipLocalsInit]
[DebuggerNonUserCode]
internal sealed class TimingWheel : IActivatable
{
    #region Fields

    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly TimingWheelOptions s_options = ConfigurationManager.Instance.Get<TimingWheelOptions>();
    private static readonly ObjectPoolManager s_poolManager = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private readonly int _tickMs;
    private readonly int _wheelSize;
    private readonly int _idleTimeoutMs;

    /// <summary>
    /// Mask is used only when WheelSize is a power of two.
    /// </summary>
    private readonly int _mask;
    private readonly bool _useMask;

    /// <summary>
    /// One bucket per slot in the timing wheel. 
    /// Uses a custom MPSC structure to avoid the overhead of ConcurrentQueue.
    /// </summary>
    private readonly MpscBucket[] _wheel;

    /// <summary>
    /// Maps each active connection to the <em>expected</em> version of its live <see cref="TimeoutTask"/>.
    /// <para>
    /// When a task is re-scheduled, its <see cref="TimeoutTask.Version"/> is incremented and
    /// this dictionary is updated to match. Any older task still sitting in a wheel bucket will
    /// carry a stale version and be discarded (and returned to the pool) by <see cref="RUN_LOOP"/>
    /// the next time it is dequeued.
    /// </para>
    /// <para>
    /// When <see cref="Unregister"/> is called, the entry is removed entirely. The loop detects
    /// the missing key via <c>TryGetValue</c> -> <c>false</c> and returns the task to the pool
    /// without ever touching <c>task.Conn</c> after it might have been reset.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<IConnection, int> _active;

    private int _activeListeners;
    private long _tick;
    private int _disposed;
    private IWorkerHandle? _worker;
    private CancellationTokenSource? _cts;

    #endregion Fields

    #region Nested types

    /// <summary>
    /// Represents a single timeout task in the wheel.
    /// One instance is reused per registered connection via the object pool.
    /// </summary>
    private sealed class TimeoutTask : IPoolable
    {
        public IConnection? Conn;
        public int Rounds;
        public int Version;

        /// <summary>
        /// Pointer used by the MPSC bucket structure to maintain a linked list 
        /// without per-enqueue allocations.
        /// </summary>
        internal TimeoutTask? Next;

        public void ResetForPool()
        {
            Conn = null;
            Rounds = 0;
            Version = 0;
            Next = null;
        }
    }

    /// <summary>
    /// A lightweight, lock-free Multi-Producer Single-Consumer bucket.
    /// Producers enqueue new tasks atomically via Interlocked.Exchange.
    /// The timing wheel (consumer) drains the entire bucket in O(1).
    /// </summary>
    private struct MpscBucket
    {
        private TimeoutTask? _head;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(TimeoutTask task)
        {
            TimeoutTask? oldHead;
            do
            {
                oldHead = Volatile.Read(ref _head);
                task.Next = oldHead;
            } while (Interlocked.CompareExchange(ref _head, task, oldHead) != oldHead);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeoutTask? DequeueAll()
        {
            return Interlocked.Exchange(ref _head, null);
        }
    }

    #endregion Nested types

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="TimingWheel"/> class
    /// using values from <see cref="TimingWheelOptions"/> via <see cref="ConfigurationManager"/>.
    /// </summary>
    public TimingWheel()
    {
        s_options.Validate();

        PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();
        options.Validate();

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<TimeoutTask>(options.TimeoutTaskCapacity);

        // Preallocate objects in the pools so the wheel does not pay allocation
        // cost on the first few timeout registrations.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<TimeoutTask>(options.TimeoutTaskPreallocate);

        _wheelSize = s_options.BucketCount;
        _tickMs = s_options.TickDuration;
        _idleTimeoutMs = s_options.IdleTimeoutMs;

        _useMask = (_wheelSize & (_wheelSize - 1)) == 0 && _wheelSize > 0;
        _mask = _useMask ? (_wheelSize - 1) : 0;

        _wheel = new MpscBucket[_wheelSize];

        // Value = expected Version of the live task for this connection.
        _active = new ConcurrentDictionary<IConnection, int>(
            Environment.ProcessorCount * 2,
            1024);

        _disposed = 0;
    }

    #endregion Ctor

    #region IActivatable

    /// <summary>
    /// Starts the background timing loop if it is not already running.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, nameof(TimingWheel));

        if (Interlocked.Increment(ref _activeListeners) > 1)
        {
            return;
        }

        CancellationTokenSource linkedCts = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();

        _cts = linkedCts;

        _worker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{TaskNaming.Tags.Time}.{TaskNaming.Tags.Wheel}",
            group: TaskNaming.Tags.Time,
            work: async (ctx, ct) => await this.RUN_LOOP(ctx, ct).ConfigureAwait(false),
            options: new WorkerOptions
            {
                Tag = TaskNaming.Tags.Wheel,
                IdType = SnowflakeType.System,
                CancellationToken = linkedCts.Token,
                RetainFor = TimeSpan.Zero
            }
        );

        s_logger?.Info(
            $"[NW.{nameof(TimingWheel)}:{nameof(Activate)}] activated (ref={_activeListeners}) " +
            $"wheelsize={_wheelSize} tick={_tickMs}ms idle={_idleTimeoutMs}ms mask={_useMask}");
    }

    /// <summary>
    /// Stops the background timing loop and drains all buckets back to the pool.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        int count = Interlocked.Decrement(ref _activeListeners);
        if (count > 0)
        {
            return;
        }

        // Clip to zero in case of mismatched Deactivate calls
        if (count < 0)
        {
            _ = Interlocked.Exchange(ref _activeListeners, 0);
            return;
        }

        CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);
        if (cts is null)
        {
            return;
        }

        if (_worker != null)
        {
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(_worker.Id);
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }
        try { _worker?.Dispose(); } catch { }

        this.DRAIN_AND_RELEASE_ALL_BUCKETS();
        this.CLEAR_ACTIVE_REGISTRATIONS();

        s_logger?.Info($"[NW.{nameof(TimingWheel)}:{nameof(Deactivate)}] deactivated");
    }

    #endregion IActivatable

    #region Public APIs

    /// <summary>
    /// Registers a connection for idle-timeout monitoring.
    /// </summary>
    /// <param name="connection">The connection to monitor.</param>
    /// <remarks>
    /// If the connection is already registered the call is a no-op.
    /// The method subscribes to <see cref="IConnection.OnCloseEvent"/> once so that
    /// the connection is automatically unregistered when it closes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Register(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Fast-path: already registered or raced with another register call.
        // TryAdd makes duplicate Register calls effectively no-ops.
        if (!_active.TryAdd(connection, 0))
        {
            return;
        }

        TimeoutTask? task = null;
        bool subscribed = false;
        bool queued = false;

        try
        {
            task = s_poolManager.Get<TimeoutTask>();
            task.Conn = connection;
            // ResetForPool guarantees this starts at zero, but write it explicitly
            // so the initial version is obvious in the code path.
            task.Version = 0;

            long baseTick = Volatile.Read(ref _tick);
            long ticks = Math.Max(1, _idleTimeoutMs / (long)_tickMs);

            int bucket = _useMask
                ? (int)((baseTick + ticks) & _mask)
                : (int)((baseTick + ticks) % _wheelSize);

            task.Rounds = (int)(ticks / _wheelSize);

            connection.OnCloseEvent += this.OnConnectionClosed;
            subscribed = true;

            _wheel[bucket].Enqueue(task);
            queued = true;
        }
        catch
        {
            if (task is not null && !queued)
            {
                s_poolManager.Return(task);
            }

            if (subscribed)
            {
                connection.OnCloseEvent -= this.OnConnectionClosed;
            }

            _ = _active.TryRemove(connection, out _);
            throw;
        }
    }

    /// <summary>
    /// Removes a connection from idle-timeout monitoring.
    /// </summary>
    /// <param name="connection">The connection to stop monitoring.</param>
    /// <remarks>
    /// <b>Important — pool ownership:</b> this method deliberately does <em>not</em> return
    /// the <see cref="TimeoutTask"/> to the pool. The task may still be sitting in a wheel
    /// bucket. Returning it here would let the pool reset <c>task.Conn</c> to <c>null</c>
    /// while <see cref="RUN_LOOP"/> could be about to read it, causing a
    /// <see cref="NullReferenceException"/>.
    /// <para>
    /// Instead, removing the entry from <c>_active</c> is sufficient: when the loop next
    /// dequeues the task, <c>TryGetValue</c> returns <c>false</c> and the loop returns the
    /// task to the pool itself — safely, after the task is no longer in any queue.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Unregister(IConnection connection)
    {
        if (connection is null)
        {
            return;
        }

        // Remove from _active so RUN_LOOP knows this task is dead.
        // Do NOT call s_poolManager.Return here — the task is still in the wheel queue.
        // RUN_LOOP will Return it once it dequeues it and finds no matching _active entry.
        if (_active.TryRemove(connection, out _))
        {
            connection.OnCloseEvent -= this.OnConnectionClosed;
        }
    }

    /// <summary>
    /// Releases resources and stops the background loop. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        this.Deactivate();
        _active.Clear();
    }

    #endregion Public APIs

    #region Loop

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task RUN_LOOP(
        IWorkerContext ctx,
        CancellationToken ct)
    {
        _ = Interlocked.Exchange(ref _tick, 0);
        long startTime = Clock.MonoTicksNow();
        long lastProcessedTick = -1;

        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(_tickMs));

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                ctx.Beat();

                long currentMono = Clock.MonoTicksNow();
                double elapsedMs = Clock.MonoTicksToMilliseconds(currentMono - startTime);
                long expectedTick = (long)(elapsedMs / _tickMs);

                // Catch up if we've missed any ticks due to system load or loop delay.
                while (lastProcessedTick < expectedTick)
                {
                    lastProcessedTick++;
                    long tickToProcess = lastProcessedTick;

                    int bucketIndex = _useMask
                        ? (int)(tickToProcess & _mask)
                        : (int)(tickToProcess % _wheelSize);

                    // Update the public tick count so that new registrations calculate
                    // their destination bucket based on the current logical time.
                    _ = Interlocked.Exchange(ref _tick, tickToProcess);

                    TimeoutTask? task = _wheel[bucketIndex].DequeueAll();

                    while (task is not null)
                    {
                        TimeoutTask? next = task.Next;

                        // ── Defensive null-guard ──────────────────────────────────────────
                        if (task.Conn is null)
                        {
                            s_poolManager.Return(task);
                            task = next;
                            continue;
                        }

                        // ── Stale-task check ──────────────────────────────────────────────
                        if (!_active.TryGetValue(task.Conn, out int liveVersion)
                            || liveVersion != task.Version)
                        {
                            s_poolManager.Return(task);
                            task = next;
                            continue;
                        }

                        // ── Rounds remaining ──────────────────────────────────────────────
                        if (task.Rounds > 0)
                        {
                            task.Rounds--;
                            _wheel[bucketIndex].Enqueue(task);
                            task = next;
                            continue;
                        }

                        // ── Idle-time check ───────────────────────────────────────────────
                        long idleMs = Clock.UnixMillisecondsNow() - task.Conn.LastPingTime;

                        if (idleMs >= _idleTimeoutMs)
                        {
                            s_logger?.Debug(
                                $"[NW.{nameof(TimingWheel)}] timeout " +
                                $"remote={task.Conn.NetworkEndpoint?.Address} idle={idleMs}ms");

                            try
                            {
                                task.Conn.Close(force: true);
                            }
                            catch (Exception ex)
                            {
                                s_logger?.Error(
                                    $"[NW.{nameof(TimingWheel)}] close-error " +
                                    $"remote={task.Conn.NetworkEndpoint?.Address}", ex);
                            }

                            _ = _active.TryRemove(task.Conn, out _);
                            s_poolManager.Return(task);
                            task = next;
                            continue;
                        }

                        // ── Re-schedule ───────────────────────────────────────────────────
                        long remainingMs = _idleTimeoutMs - idleMs;
                        long ticksMore = Math.Max(1, remainingMs / _tickMs);

                        int newVersion = task.Version + 1;
                        task.Version = newVersion;
                        task.Rounds = (int)(ticksMore / _wheelSize);

                        int nextBucket = _useMask
                            ? (int)((tickToProcess + ticksMore) & _mask)
                            : (int)((tickToProcess + ticksMore) % _wheelSize);

                        _active[task.Conn] = newVersion;
                        _wheel[nextBucket].Enqueue(task);
                        
                        task = next;
                    }

                    ctx.Advance(1);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(TimingWheel)}] loop-error", ex);
        }
    }

    #endregion Loop

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClosed(object? sender, IConnectEventArgs args)
    {
        if (args?.Connection is not null)
        {
            this.Unregister(args.Connection);
        }
    }

    /// <summary>
    /// Drains every wheel bucket and returns all tasks that have not yet been
    /// pool-reset (<c>Conn != null</c>) back to the pool.
    /// Called on <see cref="Deactivate"/> after the loop has been cancelled.
    /// </summary>
    private void DRAIN_AND_RELEASE_ALL_BUCKETS()
    {
        for (int i = 0; i < _wheel.Length; i++)
        {
            TimeoutTask? task = _wheel[i].DequeueAll();
            while (task is not null)
            {
                TimeoutTask? next = task.Next;

                // Only return tasks that still own a live connection reference.
                // Tasks already returned by the loop or a concurrent path have Conn
                // cleared, so they should be ignored here.
                if (task.Conn is not null)
                {
                    s_poolManager.Return(task);
                }

                task = next;
            }
        }
    }

    private void CLEAR_ACTIVE_REGISTRATIONS()
    {
        foreach (IConnection connection in _active.Keys)
        {
            try
            {
                connection.OnCloseEvent -= this.OnConnectionClosed;
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        _active.Clear();
    }

    #endregion Helpers
}
