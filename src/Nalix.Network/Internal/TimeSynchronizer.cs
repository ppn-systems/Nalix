using Nalix.Common.Logging;
using Nalix.Framework.Time;

namespace Nalix.Network.Internal;

/// <summary>
/// Manages time synchronization operations by periodically triggering time updates.
/// This class ensures time synchronization occurs at a fixed interval when enabled.
/// </summary>
internal class TimeSynchronizer(ILogger logger)
{
    private volatile System.Boolean _isRunning = false;
    private volatile System.Boolean _isTimeSyncEnabled = false;
    private readonly ILogger _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));

    /// <summary>
    /// Gets a value indicating whether the time synchronization loop is currently running.
    /// </summary>
    public System.Boolean IsRunning => this._isRunning;

    /// <summary>
    /// Gets or sets a value indicating whether time synchronization is enabled.
    /// When set to <c>true</c>, the synchronization loop will start processing if not already running.
    /// </summary>
    public System.Boolean IsTimeSyncEnabled
    {
        get => this._isTimeSyncEnabled;
        set => this._isTimeSyncEnabled = value;
    }

    /// <summary>
    /// Event that gets triggered when it's time to synchronize.
    /// The value passed is the current timestamp in milliseconds.
    /// </summary>
    public event System.Action<System.Int64>? TimeSynchronized;

    /// <summary>
    /// Runs the time synchronization loop asynchronously.
    /// The loop waits for synchronization to be enabled and then triggers the <see cref="TimeSynchronized"/> event
    /// at approximately 16ms intervals, adjusted for processing time.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task RunAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        if (this._isRunning)
        {
            this._logger.Warn("Time synchronization loop is already running.");
            return;
        }

        this._isRunning = true;

        try
        {
            if (!this._isTimeSyncEnabled)
            {
                this._logger.Debug("Waiting for time sync loop to be enabled...");
            }

            while (!this._isTimeSyncEnabled && !cancellationToken.IsCancellationRequested)
            {
                await System.Threading.Tasks.Task.Delay(10000, cancellationToken).ConfigureAwait(false);
            }

            this._logger.Info("Time sync loop enabled, starting update cycle.");

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
            this._logger.Debug("Time synchronization loop cancelled");
        }
        catch (System.Exception ex)
        {
            this._logger.Error("Time synchronization loop error: {0}", ex.Message);
        }
        finally
        {
            this._isRunning = false;
        }
    }

    /// <summary>
    /// Stops the time synchronization loop if it is currently running.
    /// </summary>
    public void Stop()
    {
        if (!this._isRunning)
        {
            return;
        }

        this._isRunning = false;
        this._logger.Info("Time synchronization loop stopped.");
    }
}