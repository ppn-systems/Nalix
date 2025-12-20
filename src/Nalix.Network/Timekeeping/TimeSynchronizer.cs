// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Internal;

namespace Nalix.Network.Timekeeping;

/// <summary>
/// Emits periodic time synchronization ticks at a target cadence (~16 ms, ~60 Hz).
/// Designed as a lightweight singleton-style service with explicit enable/disable control.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class TimeSynchronizer : System.IDisposable, IActivatable
{
    #region Constants

    /// <summary>
    /// Target period for ~60 FPS cadence.
    /// </summary>
    public static readonly System.TimeSpan DefaultPeriod = System.TimeSpan.FromMilliseconds(16);

    #endregion Constants

    #region Fields

    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private readonly System.Threading.Lock _gate = new();
    private System.Threading.CancellationTokenSource _cts;

    private int _isRunning;
    private int _isDisposed;
    private int _enabled;
    private volatile bool _fireAndForget;

    #endregion Fields

    #region Events

    /// <summary>
    /// Raised every tick with the current Unix timestamp in milliseconds.
    /// NOTE: Handlers should be lightweight. Consider enabling FireAndForget if handlers may block.
    /// </summary>
    public event System.Action<long> TimeSynchronized;

    #endregion Events

    #region Properties

    /// <summary>
    /// True if the background loop is currently running.
    /// </summary>
    public bool IsRunning => System.Threading.Volatile.Read(ref _isRunning) == 1;

    /// <summary>
    /// True if synchronization is enabled (and the loop should run).
    /// </summary>
    public bool IsTimeSyncEnabled
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
    /// <exception cref="System.ArgumentOutOfRangeException"></exception>
    public System.TimeSpan Period
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value <= System.TimeSpan.Zero)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Period must be positive.");
            }

            field = value;
            // If running, restart to apply new period
            if (IsRunning) { Restart(); }
        }
    } = DefaultPeriod;

    /// <summary>
    /// If true, handlers are dispatched to the ThreadPool to avoid blocking the tick loop.
    /// Default is false for minimal overhead.
    /// </summary>
    public bool FireAndForget
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _fireAndForget;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set => _fireAndForget = value;
    }

    #endregion Properties

    #region APIs

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSynchronizer"/> class.
    /// </summary>
    public TimeSynchronizer() => s_logger?.Debug($"[NW.{nameof(TimeSynchronizer)}] initialized");

    /// <summary>
    /// Enables synchronization and ensures the loop is running.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Activate(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _isDisposed) != 0, nameof(TimeSynchronizer));

        if (System.Threading.Interlocked.CompareExchange(ref _enabled, 1, 0) == 1)
        {
            return;
        }

        INITIALIZE_SYNC_LOOP();
    }

    /// <summary>
    /// Disables synchronization and stops the loop.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _enabled, 0, 1) == 0)
        {
            return;
        }

        TERMINATE_SYNC_LOOP();
    }

    /// <summary>
    /// Starts or restarts the loop to apply new settings.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Restart()
    {
        if (!IsTimeSyncEnabled)
        {
            return;
        }

        TERMINATE_SYNC_LOOP();

        System.Threading.Thread.Sleep(50);

        INITIALIZE_SYNC_LOOP();
    }

    #endregion APIs

    #region Dispose

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
        {
            return;
        }

        Deactivate();

        TimeSynchronized = null;

        System.GC.SuppressFinalize(this);

        s_logger?.Debug($"[NW.{nameof(TimeSynchronizer)}] disposed");
    }

    #endregion Dispose

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void INITIALIZE_SYNC_LOOP()
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
                System.Threading.Volatile.Write(ref _isRunning, 0);
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
                    using System.Threading.PeriodicTimer timer = new(Period);

                    s_logger?.Info($"[NW.{nameof(TimeSynchronizer)}] started period={Period.TotalMilliseconds:0.#}ms");

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

                        long timestamp = Clock.UnixMillisecondsNow();
                        System.Action<long> handler = TimeSynchronized;

                        if (handler is not null)
                        {
                            if (_fireAndForget)
                            {
                                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(static state =>
                                {
                                    (System.Action<long> h, long ts) = ((System.Action<long>, long))state;
                                    try
                                    {
                                        h(ts);
                                    }
                                    catch (System.Exception ex)
                                    {
                                        s_logger?.Error($"[NW.{nameof(TimeSynchronizer)}] handler-error", ex);
                                    }
                                }, (handler, timestamp), preferLocal: false);
                            }
                            else
                            {
                                try
                                {
                                    handler(timestamp);
                                }
                                catch (System.Exception ex)
                                {
                                    s_logger?.Error($"[NW.{nameof(TimeSynchronizer)}] handler-error", ex);
                                }
                            }
                        }

                        long elapsed = Clock.UnixMillisecondsNow() - timestamp;
                        if (elapsed > Period.TotalMilliseconds * 1.5)
                        {
                            s_logger?.Warn(
                                $"[NW.{nameof(TimeSynchronizer)}] tick overrun " +
                                $"elapsed={elapsed}ms period={Period.TotalMilliseconds:0.#}ms");
                        }

                        ctx?.Beat();
                    }
                }
                catch (System.OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (System.Exception ex)
                {
                    s_logger?.Error($"[NW.{nameof(TimeSynchronizer)}] loop-error", ex);
                }
                finally
                {
                    System.Threading.Volatile.Write(ref _isRunning, 0);
                    s_logger?.Info($"[NW.{nameof(TimeSynchronizer)}] stopped");
                }
            },
            options: new WorkerOptions
            {
                CancellationToken = linkedToken,
                IdType = SnowflakeType.System,
                RetainFor = System.TimeSpan.Zero,
                Tag = NetTaskNames.Sync
            }
        );
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void TERMINATE_SYNC_LOOP()
    {
        _ = System.Threading.Interlocked.Exchange(ref _isRunning, 0);

        System.Threading.CancellationTokenSource toCancel;
        lock (_gate)
        {
            toCancel = _cts;
            _cts = null;
        }

        try { toCancel?.Cancel(); } catch { }
        try { toCancel?.Dispose(); } catch { }
    }

    #endregion Private Methods
}
