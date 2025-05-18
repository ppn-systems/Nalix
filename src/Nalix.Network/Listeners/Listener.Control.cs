using Nalix.Common.Exceptions;
using Nalix.Shared.Time;

namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    public abstract void UpdateTime(long milliseconds);

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    public async System.Threading.Tasks.Task BeginListeningAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isListening) return;

        _isListening = true;
        _logger.Debug("Starting listener");
        const int maxParallelAccepts = 5;

        // Create a linked token source to combine external cancellation with Internal cancellation
        _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        System.Threading.CancellationToken linkedToken = _cts.Token;

        await _listenerLock.WaitAsync(linkedToken).ConfigureAwait(false);

        try
        {
            // Bind and Listen

            _listenerSocket.Bind(_bindEndPoint);
            _listenerSocket.Listen(Listener.SocketBacklog);

            _logger.Info("{0} online on {1}", _protocol, _port);

            // Create multiple accept tasks in parallel for higher throughput
            System.Threading.Tasks.Task updateTask = this.RunUpdateLoopAsync(linkedToken);
            System.Threading.Tasks.Task[] acceptTasks = new System.Threading.Tasks.Task[maxParallelAccepts];

            for (int i = 0; i < maxParallelAccepts; i++)
            {
                acceptTasks[i] = this.AcceptConnectionsAsync(linkedToken);
            }

            await System.Threading.Tasks.Task
                    .WhenAll(System.Linq.Enumerable.Append(acceptTasks, updateTask))
                    .ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            _logger.Info("Listener on {0} stopped", _port);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            throw new InternalErrorException($"Could not start {_protocol} on port {_port}", ex);
        }
        catch (System.Exception ex)
        {
            throw new InternalErrorException($"Critical error in listener on port {_port}", ex);
        }
        finally
        {
            try
            {
                _listenerSocket.Close();
            }
            catch { }

            _listenerLock.Release();
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void EndListening()
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);

        _cts?.Cancel();

        try
        {
            // Close the socket listener to deactivate the accept
            _listenerSocket.Close();
        }
        catch (System.Exception ex)
        {
            _logger.Error("Error closing listener socket: {0}", ex.Message);
        }

        _isListening = false;
        _logger.Info("Listener stopped.");
    }

    #region Private Methods

    private async System.Threading.Tasks.Task RunUpdateLoopAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            // Wait until enabled
            if (!_enableUpdate)
            {
                _logger.Debug("Waiting for update loop to be enabled...");
            }

            while (!_enableUpdate && !cancellationToken.IsCancellationRequested)
            {
                await System.Threading.Tasks.Task
                        .Delay(10000, cancellationToken)
                        .ConfigureAwait(false);
            }

            _logger.Info("Update loop enabled, starting update cycle.");

            // Main update loop
            while (_isListening)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long start = Clock.UnixMillisecondsNow();
                this.UpdateTime(start);

                long elapsed = Clock.UnixMillisecondsNow() - start;
                long remaining = 16 - elapsed;

                if (remaining < 16)
                {
                    await System.Threading.Tasks.Task
                            .Delay((int)remaining, cancellationToken)
                            .ConfigureAwait(false);
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            _logger.Debug("UpdateTime loop cancelled");
        }
        catch (System.Exception ex)
        {
            _logger.Error("UpdateTime loop error: {0}", ex.Message);
        }
    }

    #endregion Private Methods
}
