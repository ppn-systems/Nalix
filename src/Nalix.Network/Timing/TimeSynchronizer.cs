// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Enums;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Internal;

namespace Nalix.Network.Timing;

/// <summary>
/// Emits periodic time synchronization ticks at a target cadence (~16 ms, ~60 Hz).
/// Designed as a lightweight singleton-style service with explicit enable/disable control.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class TimeSynchronizer : System.IDisposable, IActivatable
{
    #region Constants

    /// <summary>Target period for ~60 FPS cadence.</summary>
    public static readonly System.TimeSpan DefaultPeriod = System.TimeSpan.FromMilliseconds(16);

    #endregion

    #region Fields

    private System.TimeSpan _period = DefaultPeriod;
    private readonly System.Threading.Lock _gate = new();
    [System.Diagnostics.CodeAnalysis.AllowNull] private System.Threading.CancellationTokenSource _cts;

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
    public event System.Action<System.Int64> TimeSynchronized;

    #endregion

    #region Properties

    /// <summary>True if the background loop is currently running.</summary>
    public System.Boolean IsRunning => System.Threading.Volatile.Read(ref _isRunning) == 1;

    /// <summary>True if synchronization is enabled (and the loop should run).</summary>
    public System.Boolean IsTimeSyncEnabled
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Volatile.Read(ref _enabled) == 1;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _period;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _fireAndForget;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _fireAndForget = value;
    }

    #endregion

    #region APIs

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSynchronizer"/> class.
    /// </summary>
    public TimeSynchronizer()
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(TimeSynchronizer)}] init");
    }

    /// <summary>
    /// Enables synchronization and ensures the loop is running.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Activate(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Volatile.Read(ref _enabled) == 1)
        {
            return;
        }

        System.Threading.Volatile.Write(ref _enabled, 1);
        InitializeSyncLoop();
    }

    /// <summary>
    /// Disables synchronization and stops the loop.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Volatile.Read(ref _enabled) == 0)
        {
            return;
        }

        System.Threading.Volatile.Write(ref _enabled, 0);
        TerminateSyncLoop();
    }

    /// <summary>Starts or restarts the loop to apply new settings.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Restart()
    {
        if (!IsTimeSyncEnabled)
        {
            return;
        }

        TerminateSyncLoop();
        InitializeSyncLoop();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void InitializeSyncLoop()
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

        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{NetTaskNames.Time}.{NetTaskNames.Sync}",
            group: NetTaskNames.Time,
            work: async (ctx, ct) =>
            {
                try
                {
                    using System.Threading.PeriodicTimer timer = new(_period);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Info($"[{nameof(TimeSynchronizer)}:Internal] start period={_period.TotalMilliseconds:0.#}ms");

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
                        System.Action<System.Int64> handler = TimeSynchronized;

                        if (handler is not null)
                        {
                            if (_fireAndForget)
                            {
                                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(static state =>
                                {
                                    (System.Action<System.Int64> h, System.Int64 ts) = ((System.Action<System.Int64>, System.Int64))state!;
                                    try
                                    {
                                        h(ts);
                                    }
                                    catch (System.Exception ex)
                                    {
                                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                                .Error($"[NW.{nameof(TimeSynchronizer)}:Internal] handler-error", ex);
                                    }
                                }, (handler, t0), preferLocal: false);
                            }
                            else
                            {
                                try
                                {
                                    handler(t0);
                                }
                                catch (System.Exception ex)
                                {
                                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                            .Error($"[NW.{nameof(TimeSynchronizer)}:Internal] handler-error", ex);
                                }
                            }
                        }

                        System.Int64 elapsed = Clock.UnixMillisecondsNow() - t0;
                        if (elapsed > _period.TotalMilliseconds * 1.5)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Warn($"[NW.{nameof(TimeSynchronizer)}:Internal] overrun elapsed={elapsed}ms period={_period.TotalMilliseconds:0.#}ms");
                        }

                        ctx.Beat();
                    }
                }
                catch (System.OperationCanceledException)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[NW.{nameof(TimeSynchronizer)}:Internal] cancelled");
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(TimeSynchronizer)}:Internal] loop-error", ex);
                }
                finally
                {
                    System.Threading.Volatile.Write(ref _isRunning, 0);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Info($"[NW.{nameof(TimeSynchronizer):Internal}] stop");
                }
            },
            options: new WorkerOptions
            {
                CancellationToken = linkedToken,
                IdType = SnowflakeType.System,
                RetainFor = System.TimeSpan.Zero
            }
        );
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void TerminateSyncLoop()
    {
        if (System.Threading.Interlocked.Exchange(ref _isRunning, 0) == 0)
        {
        }

        System.Threading.CancellationTokenSource toCancel;
        lock (_gate)
        {
            toCancel = _cts;
            _cts = null;
        }

        try { toCancel?.Cancel(); } catch { }
        try { toCancel?.Dispose(); } catch { }
    }

    #endregion APIs

    #region Dispose

    /// <inheritdoc/>
    [System.Diagnostics.StackTraceHidden]
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
                                .Meta($"[NW.{nameof(TimeSynchronizer)}:{nameof(Dispose)}] disposed");
    }

    #endregion
}
