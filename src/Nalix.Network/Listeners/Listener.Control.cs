using Nalix.Common.Exceptions;
using Nalix.Shared.Time;

namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    public async System.Threading.Tasks.Task StartListeningAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (Config.MaxParallel < 1)
            throw new System.InvalidOperationException("Config.MaxParallel must be at least 1.");

        if (_isRunning)
        {
            _logger.Warn("[TCP] Accept connections is already running.");
            return;
        }

        _isRunning = true;
        _logger.Debug("Starting listener");

        // Create a linked token source to combine external cancellation with Internal cancellation
        _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        System.Threading.CancellationToken linkedToken = _cts.Token;

        await _lock.WaitAsync(linkedToken).ConfigureAwait(false);

        try
        {
            _logger.Info("[TCP] {0} listening on port {1}", _protocol, Config.Port);

            // Create multiple accept tasks in parallel for higher throughput
            int acceptCount = Config.MaxParallel;

            System.Threading.Tasks.Task updateTask = _timeSyncWorker.RunAsync(linkedToken);
            System.Threading.Tasks.Task[] acceptTasks = new System.Threading.Tasks.Task[acceptCount + 1];

            for (int i = 0; i < acceptCount; i++)
            {
                acceptTasks[i] = this.AcceptConnectionsAsync(linkedToken);
            }

            acceptTasks[Config.MaxParallel + 1] = updateTask;

            await System.Threading.Tasks.Task.WhenAll(acceptTasks).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            _logger.Info("[TCP] Listener on {0} stopped", Config.Port);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            throw new InternalErrorException($"[TCP] Could not start {_protocol} on port {Config.Port}", ex);
        }
        catch (System.Exception ex)
        {
            throw new InternalErrorException($"[TCP] Critical error in listener on port {Config.Port}", ex);
        }
        finally
        {
            try
            {
                _listener.Close();
            }
            catch { }

            _lock.Release();
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void StopListening()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);

        _cts?.Cancel();

        try
        {
            // Close the socket listener to deactivate the accept
            if (_isRunning)
            {
                _listener.Close();
                _logger.Info("[TCP] Listener on {0} stopped", Config.Port);
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error("Error closing listener socket: {0}", ex.Message);
        }

        _isRunning = false;
        _logger.Info("Listener stopped.");
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    public virtual void SynchronizeTime(long milliseconds)
    { }
}