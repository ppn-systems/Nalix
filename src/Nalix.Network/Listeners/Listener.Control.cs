using Nalix.Common.Exceptions;
using Nalix.Shared.Time;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public async Task BeginListeningAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isListening) return;

        _isListening = true;
        _logger.Debug("Starting listener");
        const int maxParallelAccepts = 5;

        // Create a linked token source to combine external cancellation with Internal cancellation
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        await _listenerLock.WaitAsync(linkedToken).ConfigureAwait(false);

        try
        {
            // Bind and Listen
            _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _listenerSocket.Listen(Listener.SocketBacklog);

            _logger.Info("{0} online on {1}", _protocol, _port);

            // Create multiple accept tasks in parallel for higher throughput
            Task updateTask = this.RunUpdateLoopAsync(linkedToken);
            Task[] acceptTasks = new Task[maxParallelAccepts];

            for (int i = 0; i < maxParallelAccepts; i++)
                acceptTasks[i] = this.AcceptConnectionsAsync(linkedToken);

            await Task.WhenAll(acceptTasks.Append(updateTask)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Listener on {0} stopped", _port);
        }
        catch (SocketException ex)
        {
            throw new InternalErrorException($"Could not start {_protocol} on port {_port}", ex);
        }
        catch (Exception ex)
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
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _cts?.Cancel();

        try
        {
            // Close the socket listener to deactivate the accept
            _listenerSocket.Close();
        }
        catch (Exception ex)
        {
            _logger.Error("Error closing listener socket: {0}", ex.Message);
        }

        // Wait for the listener thread to complete with a timeout
        if (_listenerThread?.IsAlive == true)
        {
            _listenerThread.Join(TimeSpan.FromSeconds(5));
        }

        _isListening = false;
        _logger.Info("Listener stopped.");
    }

    #region Private Methods

    private async Task RunUpdateLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_isListening)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long start = Clock.UnixMillisecondsNow();
                this.UpdateTime(start);

                long elapsed = Clock.UnixMillisecondsNow() - start;
                long remaining = 16 - elapsed;

                if (remaining < 16)
                    await Task.Delay((int)remaining, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UpdateTime loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error("UpdateTime loop error: {0}", ex.Message);
        }
    }

    #endregion Private Methods
}
