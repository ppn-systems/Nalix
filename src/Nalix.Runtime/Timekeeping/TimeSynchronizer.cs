// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Runtime.Timekeeping;

/// <summary>
/// Emits periodic time synchronization ticks at a target cadence (~16 ms, ~60 Hz).
/// Designed as a lightweight singleton-style service with explicit enable/disable control.
/// </summary>
[SkipLocalsInit]
[DebuggerNonUserCode]
public sealed class TimeSynchronizer : IDisposable, IActivatable
{
    #region Constants

    /// <summary>
    /// Target period for ~60 FPS cadence.
    /// </summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromMilliseconds(16);

    #endregion Constants

    #region Fields

    private readonly Lock _gate = new();
    private readonly ManualResetEventSlim _stoppedSignal = new(true);
    private CancellationTokenSource? _cts;

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
    public event Action<long>? TimeSynchronized;

    #endregion Events

    #region Properties

    /// <summary>
    /// True if the background loop is currently running.
    /// </summary>
    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    /// <summary>
    /// True if synchronization is enabled (and the loop should run).
    /// </summary>
    public bool IsTimeSyncEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _enabled) == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value)
            {
                this.Activate();
            }
            else
            {
                this.Deactivate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the tick period. Only applied on (re)start.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public TimeSpan Period
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Period must be positive.");
            }

            field = value;
            // If running, restart to apply new period
            if (this.IsRunning) { this.Restart(); }
        }
    } = DefaultPeriod;

    /// <summary>
    /// If true, handlers are dispatched to the ThreadPool to avoid blocking the tick loop.
    /// Default is false for minimal overhead.
    /// </summary>
    public bool FireAndForget
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fireAndForget;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _fireAndForget = value;
    }

    #endregion Properties

    #region APIs

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSynchronizer"/> class.
    /// </summary>
    public TimeSynchronizer()
    {
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Source.Write(
                DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog("RT.TimeSynchronizer:Ctor", "initialized"));
        }
    }

    /// <summary>
    /// Enables synchronization and ensures the loop is running.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, nameof(TimeSynchronizer));

        if (Interlocked.CompareExchange(ref _enabled, 1, 0) == 1)
        {
            return;
        }

        this.INITIALIZE_SYNC_LOOP();
    }

    /// <summary>
    /// Disables synchronization and stops the loop.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _enabled, 0, 1) == 0)
        {
            return;
        }

        this.TERMINATE_SYNC_LOOP();
    }

    /// <summary>
    /// Starts or restarts the loop to apply new settings.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Restart()
    {
        if (!this.IsTimeSyncEnabled)
        {
            return;
        }

        this.TERMINATE_SYNC_LOOP();

        if (_stoppedSignal.IsSet)
        {
            this.INITIALIZE_SYNC_LOOP();
            return;
        }

        _ = ThreadPool.UnsafeQueueUserWorkItem(static state =>
        {
            if (state is not TimeSynchronizer self)
            {
                return;
            }

            try
            {
                if (self._stoppedSignal.Wait(TimeSpan.FromMilliseconds(50)))
                {
                    self.INITIALIZE_SYNC_LOOP();
                }
                else
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Warning,
                            new DiagnosticLog("RT.TimeSynchronizer:Activate", "restart-timeout waiting-for-previous-loop"));
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Dispose won the race.
            }
        }, this, preferLocal: false);
    }

    #endregion APIs

    #region Dispose

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
        {
            return;
        }

        this.Deactivate();

        TimeSynchronized = null;

        if (_stoppedSignal.IsSet)
        {
            _stoppedSignal.Dispose();
        }
        else
        {
            _ = ThreadPool.UnsafeQueueUserWorkItem(static state =>
            {
                if (state is not TimeSynchronizer self)
                {
                    return;
                }

                try
                {
                    if (self._stoppedSignal.Wait(TimeSpan.FromSeconds(2)))
                    {
                        self._stoppedSignal.Dispose();
                    }
                    else
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                        {
                            DiagnosticsEvents.Source.Write(
                                DiagnosticsEvents.Internal.Warning,
                                new DiagnosticLog("RT.TimeSynchronizer:Dispose", "dispose-timeout waiting-for-loop-shutdown"));
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Signal already disposed.
                }
            }, this, preferLocal: false);
        }

        GC.SuppressFinalize(this);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Source.Write(
                DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog("RT.TimeSynchronizer:Dispose", "disposed"));
        }
    }

    #endregion Dispose

    #region Private Methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void INITIALIZE_SYNC_LOOP()
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 1)
        {
            return;
        }

        CancellationToken linkedToken;
        lock (_gate)
        {
            if (_cts is not null)
            {
                Volatile.Write(ref _isRunning, 0);
                return;
            }

            _cts = new CancellationTokenSource();
            linkedToken = _cts.Token;
        }

        _stoppedSignal.Reset();

        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{TaskNaming.Tags.Time}.{TaskNaming.Tags.Sync}",
            group: TaskNaming.Tags.Time,
            work: async (ctx, ct) =>
            {
                try
                {
                    using PeriodicTimer timer = new(this.Period);

                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Information,
                            new DiagnosticLog("RT.TimeSynchronizer:Internal", $"started period={this.Period.TotalMilliseconds}ms"));
                    }

                    while (!ct.IsCancellationRequested)
                    {
                        if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                        {
                            break;
                        }

                        if (!this.IsTimeSyncEnabled)
                        {
                            continue;
                        }

                        long timestamp = Clock.UnixMillisecondsNow();
                        Action<long>? handler = TimeSynchronized;

                        if (handler is not null)
                        {
                            if (_fireAndForget)
                            {
                                _ = ThreadPool.UnsafeQueueUserWorkItem(static state =>
                                {
                                    (Action<long> h, long ts) = ((Action<long>, long))state;
                                    try
                                    {
                                        h(ts);
                                    }
                                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                                    {
                                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                                        {
                                            DiagnosticsEvents.Source.Write(
                                                DiagnosticsEvents.Internal.Error,
                                                new DiagnosticLog("RT.TimeSynchronizer:Internal", "handler-error", ex));
                                        }
                                    }
                                }, (handler, timestamp), preferLocal: false);
                            }
                            else
                            {
                                try
                                {
                                    handler(timestamp);
                                }
                                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                                {
                                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                                    {
                                        DiagnosticsEvents.Source.Write(
                                            DiagnosticsEvents.Internal.Error,
                                            new DiagnosticLog("RT.TimeSynchronizer:Internal", "handler-error", ex));
                                    }
                                }
                            }
                        }

                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                        {
                            long elapsed = Clock.UnixMillisecondsNow() - timestamp;
                            if (elapsed > this.Period.TotalMilliseconds * 1.5)
                            {
                                DiagnosticsEvents.Source.Write(
                                    DiagnosticsEvents.Internal.Warning,
                                    new DiagnosticLog("RT.TimeSynchronizer:Internal", $"tick-overrun elapsed={elapsed}ms period={this.Period.TotalMilliseconds}ms"));
                            }
                        }

                        ctx?.Beat();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Error,
                            new DiagnosticLog("RT.TimeSynchronizer:Internal", "loop-error", ex));
                    }
                }
                finally
                {
                    Volatile.Write(ref _isRunning, 0);
                    _stoppedSignal.Set();
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Information,
                            new DiagnosticLog("RT.TimeSynchronizer:Internal", "stopped"));
                    }
                }
            },
            options: new WorkerOptions
            {
                CancellationToken = linkedToken,
                IdType = SnowflakeType.System,
                RetainFor = TimeSpan.Zero,
                Tag = TaskNaming.Tags.Sync
            }
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TERMINATE_SYNC_LOOP()
    {
        _ = Interlocked.Exchange(ref _isRunning, 0);

        CancellationTokenSource? toCancel;
        lock (_gate)
        {
            toCancel = _cts;
            _cts = null;
        }

        try
        {
            toCancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignore: already disposed
        }

        try
        {
            toCancel?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
    }

    #endregion Private Methods
}
