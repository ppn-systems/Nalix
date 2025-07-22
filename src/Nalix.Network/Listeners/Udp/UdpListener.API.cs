using Nalix.Common.Exceptions;
using Nalix.Framework.Time;
using Nalix.Network.Listeners.Core;

namespace Nalix.Network.Listeners.Udp;

/// <summary>
/// Provides a base implementation for a UDP network listener, supporting asynchronous listening,
/// protocol processing, and time synchronization. Inherit from this class to implement custom UDP listeners.
/// </summary>
public abstract partial class UdpListenerBase : IListener, System.IDisposable
{
    /// <summary>
    /// Starts listening for incoming UDP datagrams and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    public async System.Threading.Tasks.Task StartListeningAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (this._isRunning)
        {
            this._logger.Warn("[UDP] Listener is already running.");
            return;
        }

        if (this._udpClient == null)
        {
            this.InitializeUdpClient();
        }

        try
        {
            this._isRunning = true;
            this._logger.Debug("Starting UDP listener");

            this._cts?.Dispose();
            this._cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this._cancellationToken = this._cts.Token;

            await this._lock.WaitAsync(this._cancellationToken).ConfigureAwait(false);

            try
            {
                this._logger.Info("[UDP] Listening on port {0}", Config.Port);

                System.Threading.Tasks.Task receiveTask = this.ReceiveDatagramsAsync(this._cancellationToken);
                await receiveTask.ConfigureAwait(false);
            }
            finally
            {
                _ = this._lock.Release();
            }
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this._logger.Info("[UDP] Listener on {0} stopped by cancellation", Config.Port);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            throw new InternalErrorException($"[UDP] Could not start on port {Config.Port}", ex);
        }
        catch (System.Exception ex)
        {
            throw new InternalErrorException($"[UDP] Critical error in listener on port {Config.Port}", ex);
        }
        finally
        {
            if (this._isRunning)
            {
                try
                {
                    this._isRunning = false;
                    this._cts?.Cancel();

                    if (this._udpClient != null)
                    {
                        this._udpClient.Close();
                        await System.Threading.Tasks.Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (System.Exception ex)
                {
                    this._logger.Error("[UDP] Error during shutdown: {0}", ex.Message);
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
    /// Stops the listener from receiving further UDP datagrams.
    /// </summary>
    public void StopListening()
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        this._cts?.Cancel();

        try
        {
            if (this._isRunning)
            {
                this._udpClient?.Close();
                this._logger.Info("[UDP] Listener on {0} stopped", Config.Port);
            }
        }
        catch (System.Exception ex)
        {
            this._logger.Error("[UDP] Error closing listener: {0}", ex.Message);
        }
        finally
        {
            this._isRunning = false;
            this._cts?.Dispose();
            this._cts = null;
        }
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    public virtual void SynchronizeTime(System.Int64 milliseconds)
    {
    }

    /// <summary>
    /// Determines whether the incoming packet is authenticated.
    /// Default returns true (i.e., trusted). Override in derived class.
    /// </summary>
    protected virtual System.Boolean IsAuthenticated(in System.Net.Sockets.UdpReceiveResult result)
        => true;
}