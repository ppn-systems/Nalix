using Nalix.Common.Exceptions;
using Nalix.Framework.Time;
using System.Net.Sockets;

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
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (Config.MaxParallel < 1)
        {
            throw new System.InvalidOperationException("Config.MaxParallel must be at least 1.");
        }

        if (this._isRunning)
        {
            this._logger.Warn("[TCP] Accept connections is already running.");
            return;
        }

        try
        {
            this._isRunning = true;
            this._logger.Debug("Starting listener");

            this._cts?.Dispose();
            this._cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            System.Threading.CancellationToken linkedToken = this._cts.Token;

            using var registration = linkedToken.Register(() =>
            {
                try
                {
                    this._listener?.Close();
                }
                catch { }
            });
            await this._lock.WaitAsync(linkedToken).ConfigureAwait(false);

            try
            {
                this._logger.Info("[TCP] {0} listening on port {1}", this._protocol, Config.Port);

                System.Collections.Generic.List<System.Threading.Tasks.Task> tasks =
                [
                    this._timeSyncWorker.RunAsync(linkedToken)
                ];

                for (System.Int32 i = 0; i < Config.MaxParallel; i++)
                {
                    System.Threading.Tasks.Task acceptTask = this.AcceptConnectionsAsync(linkedToken)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted && !linkedToken.IsCancellationRequested)
                            {
                                this._logger.Error("[TCP] Accept task failed: {0}", t.Exception?.GetBaseException().Message);
                            }
                        }, linkedToken);

                    tasks.Add(acceptTask);
                }

                await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                _ = this._lock.Release();
            }
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this._logger.Info("[TCP] Listener on {0} stopped by cancellation", Config.Port);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            throw new InternalErrorException($"[TCP] Could not start {this._protocol} on port {Config.Port}", ex);
        }
        catch (System.Exception ex)
        {
            throw new InternalErrorException($"[TCP] Critical error in listener on port {Config.Port}", ex);
        }
        finally
        {
            if (this._isRunning)
            {
                try
                {
                    this._isRunning = false;
                    this._cts?.Cancel();

                    if (this._listener != null)
                    {
                        this._listener.Close();
                        await System.Threading.Tasks.Task.Delay(200, cancellationToken)
                                                         .ConfigureAwait(false);
                    }
                }
                catch (System.Exception ex)
                {
                    this._logger.Error("[TCP] Error during shutdown: {0}", ex.Message);
                }
                finally
                {
                    this._cts?.Dispose();
                    this._cts = null;
                }
            }
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void StopListening()
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        this._cts?.Cancel();

        try
        {
            // Close the socket listener to deactivate the accept
            if (this._isRunning)
            {
                this._listener.Shutdown(SocketShutdown.Both);
                this._logger.Info($"[TCP] Listener on {Config.Port} stopped");
            }
        }
        catch (System.Exception ex)
        {
            this._logger.Error("[TCP] Error closing listener socket: {0}", ex.Message);
        }

        this._isRunning = false;
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    public virtual void SynchronizeTime(System.Int64 milliseconds)
    { }
}