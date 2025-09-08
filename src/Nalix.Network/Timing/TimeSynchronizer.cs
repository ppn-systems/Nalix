// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
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
    private System.Threading.Tasks.Task? _loopTask;
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
    public void Activate()
    {
        if (System.Threading.Volatile.Read(ref _enabled) == 1)
        {
            return;
        }

        System.Threading.Volatile.Write(ref _enabled, 1);
        StartLoopIfNeeded();
    }

    /// <summary>Disables synchronization and stops the loop.</summary>
    public void Deactivate()
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
            return; // already running
        }

        lock (_gate)
        {
            if (_cts is not null)
            {
                return; // double-check
            }

            _cts = new System.Threading.CancellationTokenSource();
            _loopTask = RunLoopAsync(_cts.Token);
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
            .Info($"[{nameof(TimeSynchronizer)}] start period={_period.TotalMilliseconds:0.#}ms");
    }

    private void StopLoop()
    {
        if (System.Threading.Interlocked.Exchange(ref _isRunning, 0) == 0)
        {
            return; // already stopped
        }

        System.Threading.CancellationTokenSource? toCancel;
        lock (_gate)
        {
            toCancel = _cts;
            _cts = null;
        }

        try { toCancel?.Cancel(); } catch { /* ignore */ }
        try { toCancel?.Dispose(); } catch { /* ignore */ }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
            .Info($"[{nameof(TimeSynchronizer)}] stop");
    }

    #endregion

    #region Core Loop

    private async System.Threading.Tasks.Task RunLoopAsync(System.Threading.CancellationToken token)
    {
        try
        {
            // PeriodicTimer handles cadence more stably than ad-hoc delays.
            using var timer = new System.Threading.PeriodicTimer(_period);

            while (!token.IsCancellationRequested)
            {
                // Wait for next tick; break if cancelled.
                if (!await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    break;
                }

                // If disabled mid-flight, skip invoking but keep loop alive until StopLoop() is called.
                if (!IsTimeSyncEnabled)
                {
                    continue;
                }

                System.Int64 t0 = Clock.UnixMillisecondsNow();

                var handler = TimeSynchronized;
                if (handler is null)
                {
                    continue;
                }

                if (_fireAndForget)
                {
                    // Dispatch without flowing ExecutionContext for minimal overhead
                    _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(static state =>
                    {
                        var (h, timestamp) = ((System.Action<System.Int64>, System.Int64))state!;
                        try { h(timestamp); }
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

                // Simple overrun detection
                System.Int64 elapsed = Clock.UnixMillisecondsNow() - t0;
                if (elapsed > _period.TotalMilliseconds * 1.5)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                        .Warn($"[{nameof(TimeSynchronizer)}] overrun elapsed={elapsed}ms period={_period.TotalMilliseconds:0.#}ms");
                }
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
        }
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
