using Nalix.Common.Logging;
using Nalix.Framework.Time;
using Nalix.Shared.Injection;
using Nalix.Shared.Injection.DI;

namespace Nalix.Network.Timing;

/// <summary>
/// Provides periodic time synchronization by emitting time events at fixed intervals.
/// </summary>
/// <remarks>
/// Designed as a singleton. Once <see cref="IsTimeSyncEnabled"/> is set to <c>true</c>, 
/// the synchronizer starts emitting ticks at ~16ms intervals via the <see cref="TimeSynchronized"/> event.
/// </remarks>
public class TimeSynchronizer : SingletonBase<TimeSynchronizer>
{
    #region Fields

    private volatile System.Boolean _isRunning = false;
    private volatile System.Boolean _isTimeSyncEnabled = false;

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
    public System.Boolean IsTimeSyncEnabled
    {
        get => this._isTimeSyncEnabled;
        set => this._isTimeSyncEnabled = value;
    }

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
        this._isRunning = false;
        this._isTimeSyncEnabled = false;

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
        if (this._isRunning)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn("Time synchronization loop is already running.");
            return;
        }

        this._isRunning = true;

        try
        {
            if (!this._isTimeSyncEnabled)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug("Waiting for time sync loop to be enabled...");
            }

            while (!this._isTimeSyncEnabled && !cancellationToken.IsCancellationRequested)
            {
                await System.Threading.Tasks.Task.Delay(10000, cancellationToken).ConfigureAwait(false);
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info("Time sync loop enabled, starting update cycle.");

            while (this._isRunning && !cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                System.Int64 current = Clock.UnixMillisecondsNow();
                this.TimeSynchronized?.Invoke(current);

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
            this._isRunning = false;
        }
    }

    /// <summary>
    /// Stops the ticking loop if currently running.
    /// </summary>
    public void StopTicking()
    {
        if (!this._isRunning)
        {
            return;
        }

        this._isRunning = false;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info("Time synchronization loop stopped.");
    }

    #endregion APIs
}