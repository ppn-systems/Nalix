using Nalix.Common.Logging;
using Nalix.Shared.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Listeners.Internal;

internal class TimeSynchronizer(ILogger logger)
{
    private volatile bool _isRunning = false;
    private volatile bool _isTimeSyncEnabled = false;
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public bool IsRunning => _isRunning;

    public bool IsTimeSyncEnabled
    {
        get => _isTimeSyncEnabled;
        set => _isTimeSyncEnabled = value;
    }

    /// <summary>
    /// Event that gets triggered when it's time to synchronize.
    /// The value passed is the current timestamp in milliseconds.
    /// </summary>
    public event Action<long>? TimeSynchronized;

    public async Task RunAsync(CancellationToken cancellationToken)
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
                await Task.Delay(10000, cancellationToken).ConfigureAwait(false);
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
                    await Task.Delay((int)remaining, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Time synchronization loop cancelled");
        }
        catch (Exception ex)
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
