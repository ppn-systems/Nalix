// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Framework.Time;
using Nalix.Network.Internal;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Timing;

/// <summary>
/// Emits periodic time synchronization ticks at a target cadence (~16 ms, ~60 Hz).
/// Designed as a lightweight singleton-style service with explicit enable/disable control.
/// </summary>
public sealed class TimeSynchronizer : System.IDisposable, IActivatable
{
    #region Constants

    /// <summary>Target period for ~60 FPS cadence.</summary>
    public static readonly System.TimeSpan DefaultPeriod = System.TimeSpan.FromMilliseconds(16);

    #endregion

    #region Fields

    private readonly System.Threading.Lock _gate = new();
    private System.Threading.CancellationTokenSource? _cts;
    private System.TimeSpan _period = DefaultPeriod;

    // Use int flags for thread-safe state (0 = false, 1 = true)
    private System.Int32 _isRunning;    // loop running flag
    private System.Int32 _isDisposed;   // disposal flag
    private System.Int32 _enabled;      // enabled flag

    // Optional: fire-and-forget post to ThreadPool to avoid blocking tick loop
    private volatile System.Boolean _fireAndForget;

    #endregion

    #region Events

    /// <summary>
    /// Raised every tick with the current Unix timestamp in milliseconds.
    /// NOTE: Handlers should be lightweight. Consider enabling FireAndForget if handlers may block.
    /// </summary>
    public event System.Action<System.Int64>? TimeSynchronized;

    #endregion

    #region Properties

    /// <summary>True if the background loop is currently running.</summary>
    public System.Boolean IsRunning => System.Threading.Volatile.Read(ref _isRunning) == 1;

    /// <summary>True if synchronization is enabled (and the loop should run).</summary>
    public System.Boolean IsTimeSyncEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Volatile.Read(ref _enabled) == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the tick period. Only applied on (re)start.
    /// </summary>
    public System.TimeSpan Period
    {
        get => _period;
        set
        {
            if (value <= System.TimeSpan.Zero)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Period must be positive.");
            }

            _period = value;
            // If running, restart to apply new period
            if (IsRunning) { Restart(); }
        }
    }

    /// <summary>
    /// If true, handlers are dispatched to the ThreadPool to avoid blocking the tick loop.
    /// Default is false for minimal overhead.
    /// </summary>
    public System.Boolean FireAndForget
    {
        get => _fireAndForget;
        set => _fireAndForget = value;
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSynchronizer"/> class.
    /// </summary>
    public TimeSynchronizer()
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
            .Debug($"[{nameof(TimeSynchronizer)}] init");
    }

    /// <summary>Enables synchronization and ensures the loop is running.</summary>
    public void Activate(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Volatile.Read(ref _enabled) == 1)
        {
            return;
        }

        System.Threading.Volatile.Write(ref _enabled, 1);
        StartLoopIfNeeded();
    }

    /// <summary>Disables synchronization and stops the loop.</summary>
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Volatile.Read(ref _enabled) == 0)
        {
            return;
        }

        System.Threading.Volatile.Write(ref _enabled, 0);
        StopLoop();
    }

    /// <summary>Starts or restarts the loop to apply new settings.</summary>
    public void Restart()
    {
        if (!IsTimeSyncEnabled)
        {
            return;
        }

        StopLoop();
        StartLoopIfNeeded();
    }

    private void StartLoopIfNeeded()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _isRunning, 1, 0) == 1)
        {
            return;
        }

        System.Threading.CancellationToken linkedToken;
        lock (_gate)
        {
            if (_cts is not null)
            {
                return;
            }

            _cts = new System.Threading.CancellationTokenSource();
            linkedToken = _cts.Token;
        }

        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
            name: NetNames.TimeSyncWorker(Period),
            group: NetNames.TimeSyncGroup,
            work: async (ctx, ct) =>
            {
                try
                {
                    using var timer = new System.Threading.PeriodicTimer(_period);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Info($"[{nameof(TimeSynchronizer)}] start period={_period.TotalMilliseconds:0.#}ms");

                    while (!ct.IsCancellationRequested)
                    {
                        if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                        {
                            break;
                        }

                        if (!IsTimeSyncEnabled)
                        {
                            continue;
                        }

                        System.Int64 t0 = Clock.UnixMillisecondsNow();

                        var handler = TimeSynchronized;
                        if (handler is not null)
                        {
                            if (_fireAndForget)
                            {
                                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(static state =>
                                {
                                    var (h, ts) = ((System.Action<System.Int64>, System.Int64))state!;
                                    try { h(ts); }
                                    catch (System.Exception ex)
                                    {
                                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(TimeSynchronizer)}] handler-error", ex);
                                    }
                                }, (handler, t0), preferLocal: false);
                            }
                            else
                            {
                                try { handler(t0); }
                                catch (System.Exception ex)
                                {
                                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TimeSynchronizer)}] handler-error", ex);
                                }
                            }
                        }

                        System.Int64 elapsed = Clock.UnixMillisecondsNow() - t0;
                        if (elapsed > _period.TotalMilliseconds * 1.5)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Warn($"[{nameof(TimeSynchronizer)}] overrun elapsed={elapsed}ms period={_period.TotalMilliseconds:0.#}ms");
                        }

                        ctx.Heartbeat();
                    }
                }
                catch (System.OperationCanceledException)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                        .Debug($"[{nameof(TimeSynchronizer)}] cancelled");
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                        .Error($"[{nameof(TimeSynchronizer)}] loop-error", ex);
                }
                finally
                {
                    System.Threading.Volatile.Write(ref _isRunning, 0);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Info($"[{nameof(TimeSynchronizer)}] stop");
                }
            },
            options: new WorkerOptions
            {
                CancellationToken = linkedToken,
                Tag = "timesync",
                Retention = System.TimeSpan.Zero
            }
        );
    }

    private void StopLoop()
    {
        if (System.Threading.Interlocked.Exchange(ref _isRunning, 0) == 0)
        {
        }

        System.Threading.CancellationTokenSource? toCancel;
        lock (_gate)
        {
            toCancel = _cts;
            _cts = null;
        }

        try { toCancel?.Cancel(); } catch { }
        try { toCancel?.Dispose(); } catch { }
    }

    #endregion

    #region Dispose

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        Deactivate(); // stops loop and disposes CTS
        TimeSynchronized = null;
        System.GC.SuppressFinalize(this);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[{nameof(TimeSynchronizer)}] disposed");
    }

    #endregion
}
