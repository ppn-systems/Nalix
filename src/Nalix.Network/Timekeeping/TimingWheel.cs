// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;
using Nalix.Network.Internal.Constants;

namespace Nalix.Network.Timekeeping;

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
public sealed class TimingWheel : IActivatable
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
    /// One queue per bucket (MPSC; producers = Register/reschedules, consumer = RunLoop).
    /// </summary>
    private readonly ConcurrentQueue<TimeoutTask>[] _wheel;

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
    /// the missing key via <c>TryGetValue</c> → <c>false</c> and returns the task to the pool
    /// without ever touching <c>task.Conn</c> after it might have been reset.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<IConnection, int> _active;

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
        /// <summary>The connection being monitored.</summary>
        public IConnection? Conn;

        /// <summary>
        /// Number of full wheel revolutions remaining before the task fires.
        /// </summary>
        public int Rounds;

        /// <summary>
        /// Monotonically increasing counter. Incremented on every re-schedule.
        /// <see cref="RUN_LOOP"/> discards this task when its <c>Version</c> no longer matches
        /// the value stored in <c>_active</c> for the same connection.
        /// </summary>
        public int Version;

        /// <summary>Resets all fields before returning to the pool.</summary>
        public void ResetForPool()
        {
            Conn = null;
            Rounds = 0;
            Version = 0;
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

        // Preallocate objects in the pools to improve performance and reduce latency during runtime.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<TimeoutTask>(options.TimeoutTaskPreallocate);

        _wheelSize = s_options.BucketCount;
        _tickMs = s_options.TickDuration;
        _idleTimeoutMs = s_options.IdleTimeoutMs;

        _useMask = (_wheelSize & (_wheelSize - 1)) == 0 && _wheelSize > 0;
        _mask = _useMask ? (_wheelSize - 1) : 0;

        _wheel = new ConcurrentQueue<TimeoutTask>[_wheelSize];
        for (int i = 0; i < _wheelSize; i++)
        {
            _wheel[i] = new ConcurrentQueue<TimeoutTask>();
        }

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

        if (_cts is { IsCancellationRequested: false })
        {
            return;
        }

        CancellationTokenSource linkedCts = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new CancellationTokenSource();

        _cts = linkedCts;

        _worker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{NetworkTags.Time}.{NetworkTags.Wheel}",
            group: NetworkTags.Time,
            work: async (ctx, ct) => await this.RUN_LOOP(ctx, ct).ConfigureAwait(false),
            options: new WorkerOptions
            {
                Tag = NetworkTags.Wheel,
                IdType = SnowflakeType.System,
                CancellationToken = linkedCts.Token,
                RetainFor = TimeSpan.Zero
            }
        );

        s_logger?.Info(
            $"[NW.{nameof(TimingWheel)}:{nameof(Activate)}] activated " +
            $"wheelsize={_wheelSize} tick={_tickMs}ms idle={_idleTimeoutMs}ms mask={_useMask}");
    }

    /// <summary>
    /// Stops the background timing loop and drains all buckets back to the pool.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);
        if (cts is null)
        {
            return;
        }

        if (_worker != null)
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
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

        // Fast-path: already registered.
        if (_active.ContainsKey(connection))
        {
            return;
        }

        TimeoutTask task = s_poolManager.Get<TimeoutTask>();
        task.Conn = connection;
        task.Version = 0; // ResetForPool guarantees this, but be explicit.

        long baseTick = Interlocked.Read(ref _tick);
        long ticks = Math.Max(1, _idleTimeoutMs / (long)_tickMs);

        int bucket = _useMask
            ? (int)((baseTick + ticks) & _mask)
            : (int)((baseTick + ticks) % _wheelSize);

        task.Rounds = (int)(ticks / _wheelSize);

        // _active stores the *expected* version (0) for this connection.
        // TryAdd is atomic — if two threads race here, only one wins and the
        // loser returns its freshly allocated task to the pool.
        if (_active.TryAdd(connection, 0))
        {
            connection.OnCloseEvent += this.OnConnectionClosed;
            _wheel[bucket].Enqueue(task);
        }
        else
        {
            s_poolManager.Return(task);
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

        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(_tickMs));

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                ctx.Beat();

                long tickSnapshot = Interlocked.Read(ref _tick);
                int bucketIndex = _useMask
                    ? (int)(tickSnapshot & _mask)
                    : (int)(tickSnapshot % _wheelSize);

                ConcurrentQueue<TimeoutTask> queue = _wheel[bucketIndex];

                while (queue.TryDequeue(out TimeoutTask? task))
                {
                    // ── Defensive null-guard ──────────────────────────────────────────
                    // Should never happen under normal operation, but guards against any
                    // future code path that might return a task to the pool prematurely.
                    if (task.Conn is null)
                    {
                        s_poolManager.Return(task);
                        continue;
                    }

                    // ── Stale-task check ──────────────────────────────────────────────
                    // If the connection was Unregistered (_active has no entry) OR was
                    // re-scheduled and this is an old copy (version mismatch), discard.
                    // RUN_LOOP is the sole owner responsible for returning to the pool.
                    if (!_active.TryGetValue(task.Conn, out int liveVersion)
                        || liveVersion != task.Version)
                    {
                        s_poolManager.Return(task);
                        continue;
                    }

                    // ── Rounds remaining ──────────────────────────────────────────────
                    if (task.Rounds > 0)
                    {
                        task.Rounds--;
                        queue.Enqueue(task); // stay in the same bucket for one more revolution
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
                        catch (Exception)
                        {
                            throw;
                        }

                        // Close() fires OnCloseEvent → Unregister() → _active entry removed.
                        // The task is now fully done; safe to return to pool.
                        s_poolManager.Return(task);
                        continue;
                    }

                    // ── Re-schedule ───────────────────────────────────────────────────
                    // Connection is still alive but hasn't been idle long enough yet.
                    // Bump the version so any stale copy of this task that surfaces later
                    // will be discarded by the stale-task check above.
                    long remainingMs = _idleTimeoutMs - idleMs;
                    long ticksMore = Math.Max(1, remainingMs / _tickMs);

                    int newVersion = task.Version + 1;
                    task.Version = newVersion;
                    task.Rounds = (int)(ticksMore / _wheelSize);

                    int nextBucket = _useMask
                        ? (int)((tickSnapshot + ticksMore) & _mask)
                        : (int)((tickSnapshot + ticksMore) % _wheelSize);

                    // Update _active first so that if Unregister races here, it will
                    // remove the entry and the enqueued task will be caught by stale check.
                    _active[task.Conn] = newVersion;

                    _wheel[nextBucket].Enqueue(task);
                }

                _ = Interlocked.Increment(ref _tick);
                ctx.Advance(1);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown — swallow silently.
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
            ConcurrentQueue<TimeoutTask> queue = _wheel[i];
            while (queue.TryDequeue(out TimeoutTask? task))
            {
                // Guard: skip tasks that were already returned to pool by a concurrent path.
                if (task.Conn is not null)
                {
                    s_poolManager.Return(task);
                }
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
