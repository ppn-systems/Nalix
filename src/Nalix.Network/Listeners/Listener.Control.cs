using Nalix.Common.Exceptions;
using Nalix.Shared.Time;
using System;
using System.Net;
using System.Net.Sockets;

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

        EndPoint remote = new IPEndPoint(IPAddress.Any, Config.TcpPort);

        try
        {
            // Bind and Listen

            _tcpListener.Bind(remote);
            _udpListener.Bind(remote);

            _tcpListener.Listen(SocketBacklog);

            _logger.Info("[TCP] {0} listening on port {1}", _protocol, Config.TcpPort);

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
            _logger.Info("[TCP] Listener on {0} stopped", Config.TcpPort);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            throw new InternalErrorException($"[TCP] Could not start {_protocol} on port {Config.TcpPort}", ex);
        }
        catch (System.Exception ex)
        {
            throw new InternalErrorException($"[TCP] Critical error in listener on port {Config.TcpPort}", ex);
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
            _tcpListener.Close();
        }
        catch (System.Exception ex)
        {
            _logger.Error("Error closing listener socket: {0}", ex.Message);
        }

        _isListening = false;
        _logger.Info("Listener stopped.");
    }

    #region Private Methods

    private async System.Threading.Tasks.Task ReceiveUdpLoopAsync()
    {
        byte[] buffer = new byte[Config.BufferSize];
        EndPoint remote = new IPEndPoint(IPAddress.Any, Config.UdpPort);

        while (!_cts!.Token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpListener.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remote);
                //_protocol.ProcessMessage(buffer.AsSpan(0, result.ReceivedBytes), result.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                _logger.Error("Error in UDP listener: {0}", ex);
            }
        }
    }

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
