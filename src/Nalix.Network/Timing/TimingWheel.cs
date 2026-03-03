// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Concurrency;
using Nalix.Common.Connection;
using Nalix.Common.Diagnostics;
using Nalix.Common.Enums;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Infrastructure.Connection;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;
using Nalix.Network.Internal;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Timing;

/// <summary>
/// Provides an ultra-lightweight <b>Hashed Wheel Timer</b> for idle connection cleanup,
/// optimized to minimize allocations and avoid duplicate handlers.
/// </summary>
/// <remarks>
/// <para>
/// The timer periodically inspects registered <see cref="IConnection"/> instances and closes those
/// that have been idle longer than the configured threshold
/// (<see cref="TimingWheelOptions.TcpIdleTimeout"/>).
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
/// </remarks>
/// <example>
/// <code>
/// var timer = new TimingWheel();
/// timer.Activate();
/// timer.Register(connection); // connection implements IConnection with LastPingTime
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
    /// Tracks currently active connections and their single live <see cref="TimeoutTask"/>.
    /// Prevents acting on closed/unregistered connections and eliminates duplicate handlers.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, TimeoutTask> _active;

    private System.Int64 _tick;
    private System.Int32 _disposed;
    private System.Threading.CancellationTokenSource _cts;

    #endregion Fields

    #region Nested types

    /// <summary>
    /// Represents a single timeout task in the wheel.
    /// One instance is reused for the lifetime of a registered connection.
    /// </summary>
    private sealed class TimeoutTask : IPoolable
    {
        /// <summary>The connection being monitored.</summary>
        public IConnection Conn = default!;

        /// <summary>
        /// Number of full wheel revolutions remaining before the task should execute.
        /// </summary>
        public System.Int32 Rounds;

        /// <summary>Resets fields for pooling reuse.</summary>
        public void ResetForPool()
        {
            Conn = default!;
            Rounds = 0;
        }
    }

    #endregion Nested types

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="TimingWheel"/> class
    /// using values from <see cref="TimingWheelOptions"/> via <see cref="ConfigurationManager"/>.
    /// </summary>
    /// <remarks>
    /// If <c>WheelSize</c> is a power of two, a bitmask is used instead of modulo
    /// to reduce arithmetic cost when computing bucket indexes.
    /// </remarks>
    public TimingWheel()
    {
        s_options.Validate();

        _wheelSize = s_options.BucketCount;
        _tickMs = s_options.TickDuration;
        _idleTimeoutMs = s_options.TcpIdleTimeout;

        _useMask = (_wheelSize & (_wheelSize - 1)) == 0 && _wheelSize > 0;
        _mask = _useMask ? (_wheelSize - 1) : 0;

        _wheel = new System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>[_wheelSize];
        for (System.Int32 i = 0; i < _wheelSize; i++)
        {
            _wheel[i] = new System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>();
        }

        _active = new System.Collections.Concurrent.ConcurrentDictionary<IConnection, TimeoutTask>(
            System.Environment.ProcessorCount * 2,
            1024);

        _disposed = 0;
    }

    #endregion Ctor

    #region IActivatable

    /// <summary>
    /// Starts the background timing loop if it is not already running.
    /// </summary>
    /// <remarks>
    /// This method is idempotent. Subsequent calls have no effect while the timer is active.
    /// A dedicated long-running task is used to minimize scheduling jitter.
    /// </remarks>
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

        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
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
    /// Stops the background timing loop and releases any queued tasks back to the pool.
    /// </summary>
    /// <remarks>
    /// The method waits briefly for the loop to finish (<c>~2s</c>) and then drains all buckets
    /// to release pooled items early.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        var cts = System.Threading.Interlocked.Exchange(ref _cts, null);
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }

        DRAIN_AND_RELEASE_ALL_BUCKETS();

        s_logger?.Info($"[NW.{nameof(TimingWheel)}:{nameof(Deactivate)}] deactivated");
    }

    #endregion IActivatable

    #region Public APIs

    /// <summary>
    /// Registers a connection for idle monitoring.
    /// </summary>
    /// <param name="connection">The connection to monitor for idle timeout.</param>
    /// <remarks>
    /// If the connection is already registered, the call is ignored.
    /// The method attaches to <see cref="IConnection.OnCloseEvent"/> once to auto-unregister on close.
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

        if (_active.ContainsKey(connection))
        {
            return;
        }

        TimeoutTask task = s_poolManager.Get<TimeoutTask>();
        task.Conn = connection;

        System.Int64 baseTick = System.Threading.Interlocked.Read(ref _tick);
        System.Int64 ticks = System.Math.Max(1, _idleTimeoutMs / (System.Int64)_tickMs);

        System.Int32 bucket = _useMask
            ? (System.Int32)((baseTick + ticks) & _mask)
            : (System.Int32)((baseTick + ticks) % _wheelSize);

        task.Rounds = (System.Int32)(ticks / _wheelSize);

        if (_active.TryAdd(connection, task))
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
    /// Unregisters a connection so it is no longer considered for idle timeout.
    /// </summary>
    /// <param name="connection">The connection to remove from monitoring.</param>
    /// <remarks>
    /// This method is called automatically when the connection raises <see cref="IConnection.OnCloseEvent"/>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Unregister(IConnection connection)
    {
        if (connection is null)
        {
            return;
        }

        if (_active.TryRemove(connection, out _))
        {
            connection.OnCloseEvent -= OnConnectionClosed;
        }
    }

    /// <summary>
    /// Releases resources used by the timer and stops its background loop.
    /// </summary>
    /// <remarks>
    /// Equivalent to calling <see cref="Deactivate"/>. Safe to call multiple times.
    /// </remarks>
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
                System.Int64 tickSnapshot = System.Threading.Interlocked.Read(ref _tick);
                System.Int32 bucketIndex = _useMask
                    ? (System.Int32)(tickSnapshot & _mask)
                    : (System.Int32)(tickSnapshot % _wheelSize);

                var queue = _wheel[bucketIndex];

                while (queue.TryDequeue(out TimeoutTask task))
                {
                    if (!_active.TryGetValue(task.Conn, out var liveTask) || !ReferenceEquals(task, liveTask))
                    {
                        s_poolManager.Return(task);
                        continue;
                    }

                    if (task.Rounds > 0)
                    {
                        task.Rounds--;
                        queue.Enqueue(task);
                        continue;
                    }

                    System.Int64 idleMs = Clock.UnixMillisecondsNow() - task.Conn.LastPingTime;

                    if (idleMs >= _idleTimeoutMs)
                    {
                        s_logger?.Debug($"[NW.{nameof(TimingWheel)}] timeout remote={task.Conn.EndPoint?.Address} idle={idleMs}ms");

                        try
                        {
                            task.Conn.Close(force: true);
                        }
                        catch (System.Exception ex)
                        {
                            s_logger?.Warn($"[NW.{nameof(TimingWheel)}] close-error remote={task.Conn.EndPoint?.Address}", ex);
                        }

                        s_poolManager.Return(task);
                        continue;
                    }

                    System.Int64 remainingMs = _idleTimeoutMs - idleMs;
                    System.Int64 ticksMore = System.Math.Max(1, remainingMs / _tickMs);

                    task.Rounds = (System.Int32)(ticksMore / _wheelSize);

                    System.Int32 nextBucket = _useMask
                        ? (System.Int32)((tickSnapshot + ticksMore) & _mask)
                        : (System.Int32)((tickSnapshot + ticksMore) % _wheelSize);

                    _wheel[nextBucket].Enqueue(task);
                }

                System.Threading.Interlocked.Increment(ref _tick);

                ctx?.Beat();
                ctx?.Advance(1);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(TimingWheel)}] loop-error", ex);
        }
    }

    #endregion Loop

    #region Helpers

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClosed(System.Object sender, IConnectEventArgs args)
    {
        if (args?.Connection is not null)
        {
            Unregister(args.Connection);
        }
    }

    private void DRAIN_AND_RELEASE_ALL_BUCKETS()
    {
        for (System.Int32 i = 0; i < _wheel.Length; i++)
        {
            var queue = _wheel[i];
            while (queue.TryDequeue(out TimeoutTask task))
            {
                task.ResetForPool();
                s_poolManager.Return(task);
            }
        }
    }

    #endregion Helpers
}