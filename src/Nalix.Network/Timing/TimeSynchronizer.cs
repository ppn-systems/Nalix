// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Framework.Time;
using Nalix.Shared.Injection;

namespace Nalix.Network.Timing;

/// <summary>
/// Provides periodic time synchronization by emitting time events at fixed intervals.
/// </summary>
/// <remarks>
/// Designed as a singleton. Once <see cref="IsTimeSyncEnabled"/> is set to <c>true</c>, 
/// the synchronizer starts emitting ticks at ~16ms intervals via the <see cref="TimeSynchronized"/> event.
/// </remarks>
public class TimeSynchronizer : System.IDisposable
{
    #region Fields

    private System.Boolean _disposed;
    private System.Boolean _isRunning;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Indicates whether the synchronization loop is currently running.
    /// </summary>
    public System.Boolean IsRunning => this._isRunning;

    /// <summary>
    /// Gets or sets a value indicating whether time synchronization is enabled.
    /// When enabled, the background loop will start ticking periodically if not already active.
    /// </summary>
    public System.Boolean IsTimeSyncEnabled { get; set; }

    /// <summary>
    /// Occurs at every synchronization interval.
    /// Subscribers receive the current Unix timestamp in milliseconds.
    /// </summary>
    public event System.Action<System.Int64>? TimeSynchronized;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSynchronizer"/> class.
    /// </summary>
    public TimeSynchronizer()
    {
        // Ensure the singleton instance is created
        _disposed = false;
        _isRunning = false;

        this.IsTimeSyncEnabled = false;

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug("TimeSynchronizer initialized.");
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Starts the time synchronization loop asynchronously.
    /// Emits <see cref="TimeSynchronized"/> events every ~16 milliseconds.
    /// The loop only starts if <see cref="IsTimeSyncEnabled"/> is set to <c>true</c>.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous loop operation.</returns>
    public async System.Threading.Tasks.Task StartTickLoopAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        if (System.Threading.Interlocked.CompareExchange(
            ref System.Runtime.CompilerServices.Unsafe.As<
                System.Boolean, System.Int32>(ref _isRunning), 1, 0) == 1)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn("Time synchronization loop is already running.");
            return;
        }

        try
        {
            if (!this.IsTimeSyncEnabled)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug("Waiting for time sync loop to be enabled...");
            }

            while (!this.IsTimeSyncEnabled && !cancellationToken.IsCancellationRequested)
            {
                await System.Threading.Tasks.Task.Delay(10000, cancellationToken).ConfigureAwait(false);
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info("Time sync loop enabled, starting update cycle.");

            while (this._isRunning && !cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                System.Int64 current = Clock.UnixMillisecondsNow();

                var handler = TimeSynchronized;
                handler?.Invoke(current);

                System.Int64 elapsed = Clock.UnixMillisecondsNow() - current;
                System.Int64 remaining = 16 - elapsed;

                if (remaining > 0)
                {
                    await System.Threading.Tasks.Task.Delay((System.Int32)remaining, cancellationToken)
                                                     .ConfigureAwait(false);
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("Time synchronization loop cancelled");
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error("Time synchronization loop error: {0}", ex.Message);
        }
        finally
        {
            _ = System.Threading.Interlocked.Exchange(
                ref System.Runtime.CompilerServices.Unsafe.As<
                    System.Boolean, System.Int32>(ref _isRunning), 0);
        }
    }

    /// <summary>
    /// Stops the ticking loop if currently running.
    /// </summary>
    public void StopTicking()
    {
        if (System.Threading.Interlocked.Exchange(
            ref System.Runtime.CompilerServices.Unsafe.As<
                System.Boolean, System.Int32>(ref _isRunning), 0) == 0)
        {
            return;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info("Time synchronization loop stopped.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(
            ref System.Runtime.CompilerServices.Unsafe.As<
                System.Boolean, System.Int32>(ref _disposed), 1) == 1)
        {
            return;
        }

        this.StopTicking();
        this.TimeSynchronized = null;
        System.GC.SuppressFinalize(this);
    }

    #endregion APIs
}