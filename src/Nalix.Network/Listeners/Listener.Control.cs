using Nalix.Common.Exceptions;
using Nalix.Shared.Time;

namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
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
            _tcpListener.Close();
        }
        catch (System.Exception ex)
        {
            _logger.Error("Error closing listener socket: {0}", ex.Message);
        }

        _isListening = false;
        _logger.Info("Listener stopped.");
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    public abstract void SynchronizeTime(long milliseconds);

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

        if (_isListening) return;

        _isListening = true;
        _logger.Debug("Starting listener");

        // Create a linked token source to combine external cancellation with Internal cancellation
        _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        System.Threading.CancellationToken linkedToken = _cts.Token;

        await _listenerLock.WaitAsync(linkedToken).ConfigureAwait(false);

        System.Net.EndPoint remote = new System.Net.IPEndPoint(System.Net.IPAddress.Any, Config.Port);

        try
        {
            // Bind and Listen
            _tcpListener.Bind(remote);
            _udpListener.Bind(remote);

            _tcpListener.Listen(SocketBacklog);

            _logger.Info("[TCP] {0} listening on port {1}", _protocol, Config.Port);

            // Create multiple accept tasks in parallel for higher throughput
            int acceptCount = Config.MaxParallel;

            System.Threading.Tasks.Task updateTask = this.RunTimeSyncLoopAsync(linkedToken);
            System.Threading.Tasks.Task receiveTask = this.RunUdpReceiveLoopAsync(linkedToken);
            System.Threading.Tasks.Task[] acceptTasks = new System.Threading.Tasks.Task[acceptCount + 2];

            for (int i = 0; i < acceptCount; i++)
            {
                acceptTasks[i] = this.AcceptConnectionsAsync(linkedToken);
            }

            acceptTasks[Config.MaxParallel] = updateTask;
            acceptTasks[Config.MaxParallel + 1] = receiveTask;

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
                _tcpListener.Close();
            }
            catch { }

            _listenerLock.Release();
        }
    }

    #region Private Methods

    private async System.Threading.Tasks.Task RunTimeSyncLoopAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            // Wait until enabled
            if (!_isUpdateEnable)
            {
                _logger.Debug("Waiting for update loop to be enabled...");
            }

            while (!_isUpdateEnable && !cancellationToken.IsCancellationRequested)
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
                this.SynchronizeTime(start);

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
            _logger.Debug("SynchronizeTime loop cancelled");
        }
        catch (System.Exception ex)
        {
            _logger.Error("SynchronizeTime loop error: {0}", ex.Message);
        }
    }

    private async System.Threading.Tasks.Task RunUdpReceiveLoopAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        if (_isUdpEnabled) return;

        _logger.Info("[UDP] {0} listening on port {1}", _protocol, Config.Port);

        byte[] buffer = new byte[Config.BufferSize];
        System.Net.EndPoint remote = new System.Net.IPEndPoint(System.Net.IPAddress.Any, Config.Port);

        while (_isListening)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                System.Net.Sockets.SocketReceiveFromResult result = await _udpListener.ReceiveFromAsync(
                    new System.ArraySegment<byte>(buffer),
                    System.Net.Sockets.SocketFlags.None, remote);

                _protocol.ProcessMessage(System.MemoryExtensions.AsSpan(buffer, 0, result.ReceivedBytes));
            }
            catch (System.OperationCanceledException)
            {
                _logger.Info("[UDP] Listener on {0} stopped", Config.Port);
            }
            catch (System.Exception ex)
            {
                _logger.Error("[UDP] Listener Ex: {0}", ex);
            }
        }
    }

    #endregion Private Methods
}
