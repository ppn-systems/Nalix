// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Tasks;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Framework.Time;
using Nalix.Network.Configurations;
using Nalix.Network.Internal;
using Nalix.Shared.Configuration;
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
/// (<see cref="TimingWheelOptions.TcpIdleTimeoutMs"/>).
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
public sealed class TimingWheel : System.IDisposable, IActivatable
{
    #region Fields

    private static readonly TimingWheelOptions IdleTimeoutOptions =
        ConfigurationManager.Instance.Get<TimingWheelOptions>();

    private readonly System.Int32 TickMs;
    private readonly System.Int32 WheelSize;
    private readonly System.Int32 IdleTimeoutMs;

    // Mask is used only when WheelSize is a power of two.
    private readonly System.Int32 _mask;
    private readonly System.Boolean _useMask;

    // One queue per bucket (MPSC; producers = Register/reschedules, consumer = RunLoop).
    private readonly System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>[] Wheel;

    /// <summary>
    /// Tracks currently active connections and their single live <see cref="TimeoutTask"/>.
    /// Prevents acting on closed/unregistered connections and eliminates duplicate handlers.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, TimeoutTask> Active =
        new(System.Environment.ProcessorCount * 2, 1024);

    // Tick advances by 1 each timer period.
    private System.Int64 _tick;

    // Lifecycle
    private System.Threading.CancellationTokenSource? _cts; // null when not running

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
    /// using values from <see cref="IdleTimeoutOptions"/> via <see cref="ConfigurationManager"/>.
    /// </summary>
    /// <remarks>
    /// If <c>WheelSize</c> is a power of two, a bitmask is used instead of modulo
    /// to reduce arithmetic cost when computing bucket indexes.
    /// </remarks>
    public TimingWheel()
    {
        WheelSize = IdleTimeoutOptions.WheelSize;
        TickMs = IdleTimeoutOptions.TickDurationMs;
        IdleTimeoutMs = IdleTimeoutOptions.TcpIdleTimeoutMs;

        // Prefer mask if power-of-two wheel size.
        _useMask = (WheelSize & (WheelSize - 1)) == 0 && WheelSize > 0;
        _mask = _useMask ? (WheelSize - 1) : 0;

        Wheel = new System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>[WheelSize];
        for (System.Int32 i = 0; i < WheelSize; i++)
        {
            Wheel[i] = new System.Collections.Concurrent.ConcurrentQueue<TimeoutTask>();
        }
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
    public void Activate(System.Threading.CancellationToken cancellationToken = default)
    {
        // If already running, skip.
        if (_cts is { IsCancellationRequested: false })
        {
            return;
        }

        var linkedCts = cancellationToken.CanBeCanceled
                    ? System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    : new System.Threading.CancellationTokenSource();

        _cts = linkedCts;
        var linkedToken = linkedCts.Token;

        // Prefer a dedicated thread to reduce pool jitter.
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
            name: NetNames.TimingWheelWorker(TickMs, WheelSize),
            group: NetNames.TimingWheelGroup,
            work: async (ctx, ct) => { await RunLoop(ctx, ct).ConfigureAwait(false); },
            options: new WorkerOptions
            {
                CancellationToken = linkedToken,
                Tag = nameof(TimingWheel),
                Retention = System.TimeSpan.Zero
            }
        );

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(TimingWheel)}] activate " +
                                      $"wheelsize={WheelSize} tick={TickMs}ms idle={IdleTimeoutMs}ms mask={_useMask}");
    }

    /// <summary>
    /// Stops the background timing loop and releases any queued tasks back to the pool.
    /// </summary>
    /// <remarks>
    /// The method waits briefly for the loop to finish (<c>~2s</c>) and then drains all buckets
    /// to release pooled items early.
    /// </remarks>
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        var cts = System.Threading.Interlocked.Exchange(ref _cts, null);
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }

        DrainAndReleaseAllBuckets();

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(TimingWheel)}] deactivate");
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
    public void Register(IConnection connection)
    {
        // Add to Active; only attach event once (on first registration).
        if (Active.ContainsKey(connection))
        {
            return;
        }

        TimeoutTask task = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                   .Get<TimeoutTask>();
        task.Conn = connection;

        System.Int64 baseTick = System.Threading.Interlocked.Read(ref _tick);
        System.Int64 ticks = System.Math.Max(1, IdleTimeoutMs / (System.Int64)TickMs);

        System.Int32 bucket = _useMask
            ? (System.Int32)((baseTick + ticks) & _mask)
            : (System.Int32)((baseTick + ticks) % WheelSize);

        task.Rounds = (System.Int32)(ticks / WheelSize);

        if (Active.TryAdd(connection, task))
        {
            connection.OnCloseEvent += OnConnectionClosed;
            Wheel[bucket].Enqueue(task);
        }
        else
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(task);
        }
    }

    /// <summary>
    /// Unregisters a connection so it is no longer considered for idle timeout.
    /// </summary>
    /// <param name="connection">The connection to remove from monitoring.</param>
    /// <remarks>
    /// This method is called automatically when the connection raises <see cref="IConnection.OnCloseEvent"/>.
    /// </remarks>
    public void Unregister(IConnection connection)
    {
        if (Active.TryRemove(connection, out _))
        {
            // Detach handler only if we previously attached.
            connection.OnCloseEvent -= OnConnectionClosed;
        }
    }

    /// <summary>
    /// Releases resources used by the timer and stops its background loop.
    /// </summary>
    /// <remarks>
    /// Equivalent to calling <see cref="Deactivate"/>. Safe to call multiple times.
    /// </remarks>
    public void Dispose() => Deactivate();

    #endregion Public APIs

    #region Loop

    // Private loop; not part of the public API surface.
    private async System.Threading.Tasks.Task RunLoop(
        IWorkerContext? ctx = null,
        System.Threading.CancellationToken ct = default)
    {
        _ = System.Threading.Interlocked.Exchange(ref _tick, 0);

        try
        {
            using var timer = new System.Threading.PeriodicTimer(System.TimeSpan.FromMilliseconds(TickMs));

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                System.Int64 tickSnapshot = System.Threading.Interlocked.Read(ref _tick);
                System.Int32 bucketIndex = _useMask
                    ? (System.Int32)(tickSnapshot & _mask)
                    : (System.Int32)(tickSnapshot % WheelSize);

                var q = Wheel[bucketIndex];

                while (q.TryDequeue(out TimeoutTask? task))
                {
                    if (!Active.TryGetValue(task.Conn, out var live) || !ReferenceEquals(task, live))
                    {
                        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Return(task);
                        continue;
                    }

                    if (task.Rounds > 0)
                    {
                        task.Rounds--;
                        q.Enqueue(task);
                        continue;
                    }

                    System.Int64 idleMs = Clock.UnixMillisecondsNow() - task.Conn.LastPingTime;

                    if (idleMs >= IdleTimeoutMs)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                            .Debug($"[{nameof(TimingWheel)}] timeout remote={task.Conn.RemoteEndPoint}, idle={idleMs}ms");

                        try { task.Conn.Close(force: true); }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Warn($"[{nameof(TimingWheel)}] close-error remote={task.Conn.RemoteEndPoint} ex={ex.Message}");
                        }

                        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Return(task);
                        continue;
                    }

                    System.Int64 remainingMs = IdleTimeoutMs - idleMs;
                    System.Int64 ticksMore = System.Math.Max(1, remainingMs / TickMs);

                    task.Rounds = (System.Int32)(ticksMore / WheelSize);

                    System.Int32 nextBucket = _useMask
                        ? (System.Int32)((tickSnapshot + ticksMore) & _mask)
                        : (System.Int32)((tickSnapshot + ticksMore) % WheelSize);

                    Wheel[nextBucket].Enqueue(task);
                }

                _ = System.Threading.Interlocked.Increment(ref _tick);

                ctx?.Beat();
            }
        }
        catch (System.OperationCanceledException) { }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[{nameof(TimingWheel)}] loop-error", ex);
        }
    }

    #endregion Loop

    #region Helpers

    private void OnConnectionClosed(System.Object? sender, IConnectEventArgs args)
    {
        if (args.Connection is null)
        {
            return;
        }

        Unregister(args.Connection);
    }

    private void DrainAndReleaseAllBuckets()
    {
        for (System.Int32 i = 0; i < Wheel.Length; i++)
        {
            var q = Wheel[i];
            while (q.TryDequeue(out TimeoutTask? t))
            {
                t.ResetForPool();
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return(t);
            }
        }
    }

    #endregion Helpers
}
