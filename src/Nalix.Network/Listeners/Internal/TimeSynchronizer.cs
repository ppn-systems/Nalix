using Nalix.Common.Logging;
using Nalix.Shared.Time;

namespace Nalix.Network.Listeners.Internal;

internal class TimeSynchronizer(ILogger logger)
{
    private volatile System.Boolean _isRunning = false;
    private volatile System.Boolean _isTimeSyncEnabled = false;
    private readonly ILogger _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));

    public System.Boolean IsRunning => _isRunning;

    public System.Boolean IsTimeSyncEnabled
    {
        get => _isTimeSyncEnabled;
        set => _isTimeSyncEnabled = value;
    }

    /// <summary>
    /// Event that gets triggered when it's time to synchronize.
    /// The value passed is the current timestamp in milliseconds.
    /// </summary>
    public event System.Action<System.Int64>? TimeSynchronized;

    public async System.Threading.Tasks.Task RunAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            _logger.Warn("Time synchronization loop is already running.");
            return;
        }

        _isRunning = true;

        try
        {
            if (!_isTimeSyncEnabled)
            {
                _logger.Debug("Waiting for time sync loop to be enabled...");
            }

            while (!_isTimeSyncEnabled && !cancellationToken.IsCancellationRequested)
            {
                await System.Threading.Tasks.Task.Delay(10000, cancellationToken).ConfigureAwait(false);
            }

            _logger.Info("Time sync loop enabled, starting update cycle.");

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long current = Clock.UnixMillisecondsNow();
                this.TimeSynchronized?.Invoke(current);

                long elapsed = Clock.UnixMillisecondsNow() - current;
                long remaining = 16 - elapsed;

                if (remaining > 0)
                {
                    await System.Threading.Tasks.Task.Delay((System.Int32)remaining, cancellationToken)
                                                     .ConfigureAwait(false);
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            _logger.Debug("Time synchronization loop cancelled");
        }
        catch (System.Exception ex)
        {
            _logger.Error("Time synchronization loop error: {0}", ex.Message);
        }
        finally
        {
            _isRunning = false;
        }
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _logger.Info("Time synchronization loop stopped.");
    }
}