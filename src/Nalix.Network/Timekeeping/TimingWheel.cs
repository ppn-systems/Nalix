// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;
using Nalix.Network.Internal;
using Nalix.Shared.Memory.Objects;

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
/// The background loop is single-consumer and advances the wheel using <see cref="System.Threading.PeriodicTimer"/>.</para>
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
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class TimingWheel : IActivatable
{
    #region Fields

    private static readonly TimingWheelOptions s_options = ConfigurationManager.Instance.Get<TimingWheelOptions>();
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_poolManager = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private readonly System.Int32 _tickMs;
    private readonly System.Int32 _wheelSize;
    private readonly System.Int32 _idleTimeoutMs;

    // Mask is used only when WheelSize is a power of two.
    private readonly System.Int32 _mask;
    private readonly System.Boolean _useMask;

    // One queue per bucket (MPSC; producers = Register/reschedules, consumer = RunLoop).
    private readonly System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>[] _wheel;

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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, System.Int32> _active;

    private System.Int64 _tick;
    private System.Int32 _disposed;
    private IWorkerHandle _worker;
    private System.Threading.CancellationTokenSource _cts;

    #endregion Fields

    #region Nested types

    /// <summary>
    /// Represents a single timeout task in the wheel.
    /// One instance is reused per registered connection via the object pool.
    /// </summary>
    private sealed class TimeoutTask : IPoolable
    {
        /// <summary>The connection being monitored.</summary>
        public IConnection Conn = default!;

        /// <summary>
        /// Number of full wheel revolutions remaining before the task fires.
        /// </summary>
        public System.Int32 Rounds;

        /// <summary>
        /// Monotonically increasing counter. Incremented on every re-schedule.
        /// <see cref="RUN_LOOP"/> discards this task when its <c>Version</c> no longer matches
        /// the value stored in <c>_active</c> for the same connection.
        /// </summary>
        public System.Int32 Version;

        /// <summary>Resets all fields before returning to the pool.</summary>
        public void ResetForPool()
        {
            Conn = default!;
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

        _wheel = new System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>[_wheelSize];
        for (System.Int32 i = 0; i < _wheelSize; i++)
        {
            _wheel[i] = new System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>();
        }

        // Value = expected Version of the live task for this connection.
        _active = new System.Collections.Concurrent.ConcurrentDictionary<IConnection, System.Int32>(
            System.Environment.ProcessorCount * 2,
            1024);

        _disposed = 0;
    }

    #endregion Ctor

    #region IActivatable

    /// <summary>
    /// Starts the background timing loop if it is not already running.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Activate(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) != 0, nameof(TimingWheel));

        if (_cts is { IsCancellationRequested: false })
        {
            return;
        }

        System.Threading.CancellationTokenSource linkedCts = cancellationToken.CanBeCanceled
            ? System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : new System.Threading.CancellationTokenSource();

        _cts = linkedCts;

        _worker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{NetTaskNames.Time}.{NetTaskNames.Wheel}",
            group: NetTaskNames.Time,
            work: async (ctx, ct) => await RUN_LOOP(ctx, ct).ConfigureAwait(false),
            options: new WorkerOptions
            {
                Tag = NetTaskNames.Wheel,
                IdType = SnowflakeType.System,
                CancellationToken = linkedCts.Token,
                RetainFor = System.TimeSpan.Zero
            }
        );

        s_logger?.Info(
            $"[NW.{nameof(TimingWheel)}:{nameof(Activate)}] activated " +
            $"wheelsize={_wheelSize} tick={_tickMs}ms idle={_idleTimeoutMs}ms mask={_useMask}");
    }

    /// <summary>
    /// Stops the background timing loop and drains all buckets back to the pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        var cts = System.Threading.Interlocked.Exchange(ref _cts, null);
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

        DRAIN_AND_RELEASE_ALL_BUCKETS();

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Register(IConnection connection)
    {
        System.ArgumentNullException.ThrowIfNull(connection);

        if (System.Threading.Volatile.Read(ref _disposed) != 0)
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

        System.Int64 baseTick = System.Threading.Interlocked.Read(ref _tick);
        System.Int64 ticks = System.Math.Max(1, _idleTimeoutMs / (System.Int64)_tickMs);

        System.Int32 bucket = _useMask
            ? (System.Int32)((baseTick + ticks) & _mask)
            : (System.Int32)((baseTick + ticks) % _wheelSize);

        task.Rounds = (System.Int32)(ticks / _wheelSize);

        // _active stores the *expected* version (0) for this connection.
        // TryAdd is atomic — if two threads race here, only one wins and the
        // loser returns its freshly allocated task to the pool.
        if (_active.TryAdd(connection, 0))
        {
            connection.OnCloseEvent += OnConnectionClosed;
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
    /// <see cref="System.NullReferenceException"/>.
    /// <para>
    /// Instead, removing the entry from <c>_active</c> is sufficient: when the loop next
    /// dequeues the task, <c>TryGetValue</c> returns <c>false</c> and the loop returns the
    /// task to the pool itself — safely, after the task is no longer in any queue.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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
            connection.OnCloseEvent -= OnConnectionClosed;
        }
    }

    /// <summary>
    /// Releases resources and stops the background loop. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        Deactivate();
        _active.Clear();
    }

    #endregion Public APIs

    #region Loop

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private async System.Threading.Tasks.Task RUN_LOOP(
        IWorkerContext ctx,
        System.Threading.CancellationToken ct)
    {
        System.Threading.Interlocked.Exchange(ref _tick, 0);

        try
        {
            using var timer = new System.Threading.PeriodicTimer(System.TimeSpan.FromMilliseconds(_tickMs));

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                ctx.Beat();

                System.Int64 tickSnapshot = System.Threading.Interlocked.Read(ref _tick);
                System.Int32 bucketIndex = _useMask
                    ? (System.Int32)(tickSnapshot & _mask)
                    : (System.Int32)(tickSnapshot % _wheelSize);

                var queue = _wheel[bucketIndex];

                while (queue.TryDequeue(out TimeoutTask task))
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
                    if (!_active.TryGetValue(task.Conn, out System.Int32 liveVersion)
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
                    System.Int64 idleMs = Clock.UnixMillisecondsNow() - task.Conn.LastPingTime;

                    if (idleMs >= _idleTimeoutMs)
                    {
                        s_logger?.Debug(
                            $"[NW.{nameof(TimingWheel)}] timeout " +
                            $"remote={task.Conn.NetworkEndpoint?.Address} idle={idleMs}ms");

                        try
                        {
                            task.Conn.Close(force: true);
                        }
                        catch (System.Exception ex)
                        {
                            s_logger?.Warn(
                                $"[NW.{nameof(TimingWheel)}] close-error " +
                                $"remote={task.Conn.NetworkEndpoint?.Address} ex={ex.Message}");
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
                    System.Int64 remainingMs = _idleTimeoutMs - idleMs;
                    System.Int64 ticksMore = System.Math.Max(1, remainingMs / _tickMs);

                    System.Int32 newVersion = task.Version + 1;
                    task.Version = newVersion;
                    task.Rounds = (System.Int32)(ticksMore / _wheelSize);

                    System.Int32 nextBucket = _useMask
                        ? (System.Int32)((tickSnapshot + ticksMore) & _mask)
                        : (System.Int32)((tickSnapshot + ticksMore) % _wheelSize);

                    // Update _active first so that if Unregister races here, it will
                    // remove the entry and the enqueued task will be caught by stale check.
                    _active[task.Conn] = newVersion;

                    _wheel[nextBucket].Enqueue(task);
                }

                System.Threading.Interlocked.Increment(ref _tick);
                ctx.Advance(1);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Expected on shutdown — swallow silently.
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(TimingWheel)}] loop-error", ex);
        }
    }

    #endregion Loop

    #region Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClosed(System.Object sender, IConnectEventArgs args)
    {
        if (args?.Connection is not null)
        {
            Unregister(args.Connection);
        }
    }

    /// <summary>
    /// Drains every wheel bucket and returns all tasks that have not yet been
    /// pool-reset (<c>Conn != null</c>) back to the pool.
    /// Called on <see cref="Deactivate"/> after the loop has been cancelled.
    /// </summary>
    private void DRAIN_AND_RELEASE_ALL_BUCKETS()
    {
        for (System.Int32 i = 0; i < _wheel.Length; i++)
        {
            var queue = _wheel[i];
            while (queue.TryDequeue(out TimeoutTask task))
            {
                // Guard: skip tasks that were already returned to pool by a concurrent path.
                if (task.Conn is not null)
                {
                    s_poolManager.Return(task);
                }
            }
        }
    }

    #endregion Helpers
}