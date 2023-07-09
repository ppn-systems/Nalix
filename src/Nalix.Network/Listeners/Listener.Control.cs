using Nalix.Common.Exceptions;
using Nalix.Framework.Time;

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

        this._isRunning = true;
        this._logger.Debug("Starting listener");

        // Create a linked token source to combine external cancellation with Internal cancellation
        this._cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        System.Threading.CancellationToken linkedToken = this._cts.Token;

        await this._lock.WaitAsync(linkedToken).ConfigureAwait(false);

        try
        {
            this._logger.Info("[TCP] {0} listening on port {1}", this._protocol, Config.Port);

            // Create multiple accept tasks in parallel for higher throughput
            System.Int32 acceptCount = Config.MaxParallel;

            System.Threading.Tasks.Task updateTask = this._timeSyncWorker.RunAsync(linkedToken);
            System.Threading.Tasks.Task[] acceptTasks = new System.Threading.Tasks.Task[acceptCount + 1];

            for (System.Int32 i = 0; i < acceptCount; i++)
            {
                acceptTasks[i] = this.AcceptConnectionsAsync(linkedToken);
            }

            acceptTasks[Config.MaxParallel + 1] = updateTask;

            await System.Threading.Tasks.Task.WhenAll(acceptTasks).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            this._logger.Info("[TCP] Listener on {0} stopped", Config.Port);
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
            try
            {
                this._listener.Close();
            }
            catch { }

            _ = this._lock.Release();
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
                this._listener.Close();
                this._logger.Info("[TCP] Listener on {0} stopped", Config.Port);
            }
        }
        catch (System.Exception ex)
        {
            this._logger.Error("Error closing listener socket: {0}", ex.Message);
        }

        this._isRunning = false;
        this._logger.Info("Listener stopped.");
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    public virtual void SynchronizeTime(System.Int64 milliseconds)
    { }
}