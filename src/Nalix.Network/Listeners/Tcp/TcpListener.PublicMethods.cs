// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Framework.Time;
using Nalix.Network.Timing;
using Nalix.Shared.Injection;

namespace Nalix.Network.Listeners.Tcp;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Port={_port}, IsListening={_isRunning}")]
public abstract partial class TcpListenerBase
{
    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    [System.Diagnostics.DebuggerStepThrough]
    public async System.Threading.Tasks.Task ActivateAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (Config.MaxParallel < 1)
        {
            throw new System.InvalidOperationException($"[{nameof(TcpListenerBase)}] Config.MaxParallel must be at least 1.");
        }

        if (this._isRunning)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(TcpListenerBase)}] Accept connections is already running.");
            return;
        }

        if (this._listener == null ||
            !this._listener.IsBound ||
            this._listener.SafeHandle.IsInvalid)
        {
            this.Initialize();
        }

        try
        {
            this._isRunning = true;
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TcpListenerBase)}] Starting listener");

            this._cts?.Dispose();
            this._cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this._cancellationToken = this._cts.Token;

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
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[{nameof(TcpListenerBase)}] {_protocol} listening on port {Config.Port}");

                System.Collections.Generic.List<System.Threading.Tasks.Task> tasks =
                [
                    InstanceManager.Instance.GetOrCreateInstance < TimeSynchronizer >().StartTickLoopAsync(linkedToken)
                ];

                for (System.Int32 i = 0; i < Config.MaxParallel; i++)
                {
                    System.Threading.Tasks.Task acceptTask = this
                        .AcceptConnectionsAsync(linkedToken)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted && !linkedToken.IsCancellationRequested)
                            {
                                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                        .Error($"[{nameof(TcpListenerBase)}] " +
                                                               $"Accept task failed: {t.Exception?.GetBaseException().Message}");
                            }
                        }, linkedToken);

                    tasks.Add(acceptTask);
                }

                await System.Threading.Tasks.Task.WhenAll(tasks)
                                                 .ConfigureAwait(false);
            }
            finally
            {
                _ = this._lock.Release();
            }
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] on {Config.Port} stopped by cancellation");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            throw new InternalErrorException(
                $"[{nameof(TcpListenerBase)}] Could not start {this._protocol} on port {Config.Port}", ex);
        }
        catch (System.Exception ex)
        {
            throw new InternalErrorException(
                $"[{nameof(TcpListenerBase)}] Critical error in listener on port {Config.Port}", ex);
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
                        this._listener?.Close();
                        await System.Threading.Tasks.Task.Delay(200, cancellationToken)
                                                         .ConfigureAwait(false);
                    }

                    InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                                            .StopTicking();
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(TcpListenerBase)}] Error during shutdown: {ex.Message}");
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
    [System.Diagnostics.DebuggerStepThrough]
    public async System.Threading.Tasks.Task DeactivateAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        this._cts?.Cancel();

        try
        {
            // Close the socket listener to deactivate the accept
            if (this._isRunning)
            {
                this._listener?.Close();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[{nameof(TcpListenerBase)}] TcpListenerBase on {Config.Port} stopped");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] Error closing listener socket: {ex.Message}");
        }
        finally
        {
            this._isRunning = false;
            this._cts?.Dispose();
            this._cts = null;
        }

        await System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    [System.Diagnostics.DebuggerStepThrough]
    public virtual void SynchronizeTime(System.Int64 milliseconds)
    { }
}