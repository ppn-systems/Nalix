// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Time;
using Nalix.Network.Connection;
using Nalix.Network.Timing;
using Nalix.Shared.Injection;

namespace Nalix.Network.Listeners.Tcp;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Port={_port}, State={State}")]
public abstract partial class TcpListenerBase
{
    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    public async System.Threading.Tasks.Task ActivateAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (Config.MaxParallel < 1)
        {
            throw new System.InvalidOperationException($"[{nameof(TcpListenerBase)}] Config.MaxParallel must be at least 1.");
        }

        // Stopped -> Starting
        if (System.Threading.Interlocked.CompareExchange(ref _state,
           (System.Int32)ListenerState.Starting,
           (System.Int32)ListenerState.Stopped) != (System.Int32)ListenerState.Stopped)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(TcpListenerBase)}] Accept connections is already running.");
            return;
        }

        if (this._listener is null || !this._listener.IsBound ||
            this._listener.SafeHandle.IsInvalid)
        {
            this.Initialize();
        }

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

        try
        {
            await this._lock.WaitAsync(linkedToken).ConfigureAwait(false);
            try
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TcpListenerBase)}] Starting listener");

                // Starting -> Running
                _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Running);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[{nameof(TcpListenerBase)}] {_protocol} listening on port {Config.Port}");

                System.Collections.Generic.List<System.Threading.Tasks.Task> tasks =
                [
                    InstanceManager.Instance.GetOrCreateInstance < TimeSynchronizer >().StartTickLoopAsync(linkedToken)
                ];

                for (System.Int32 i = 0; i < Config.MaxParallel; i++)
                {
                    System.Threading.Tasks.Task accept = this.AcceptConnectionsAsync(linkedToken);

                    _ = accept.ContinueWith(t =>
                        {
                            if (t.IsFaulted && !linkedToken.IsCancellationRequested)
                            {
                                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] Accept task failed: {t.Exception?.GetBaseException().Message}");
                            }
                        },
                        System.Threading.CancellationToken.None,
                        System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted |
                        System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                        System.Threading.Tasks.TaskScheduler.Default
                    );

                    tasks.Add(accept);
                }

                await System.Threading.Tasks.Task.WhenAll(tasks)
                                                 .ConfigureAwait(false);
            }
            finally
            {
                _ = this._lock.Release();
            }
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] on {Config.Port} stopped by cancellation");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] Could not start {this._protocol} on port {Config.Port}", ex);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}]  Critical error in listener on port {Config.Port}", ex);
        }
        finally
        {
            try
            {
                _cts?.Cancel();
                _listener?.Close();
                await System.Threading.Tasks.Task.Delay(400).ConfigureAwait(false);

                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().StopTicking();
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] ERROR during shutdown: {ex.Message}");
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
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

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                        .Debug($"[{nameof(TcpListenerBase)}] Deactivate requested on port {Config.Port}");

        // Try Running -> Stopping
        System.Int32 prev = System.Threading.Interlocked.CompareExchange(ref _state,
            (System.Int32)ListenerState.Stopping,
            (System.Int32)ListenerState.Running);

        if (prev != (System.Int32)ListenerState.Running)
        {
            prev = System.Threading.Interlocked.CompareExchange(ref _state,
                (System.Int32)ListenerState.Stopping,
                (System.Int32)ListenerState.Starting);

            if (prev != (System.Int32)ListenerState.Starting)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(TcpListenerBase)}] Deactivate called but state = {State}");
                return;
            }
        }

        var cts = System.Threading.Interlocked.Exchange(ref this._cts, null);
        try { cts?.Cancel(); } catch { /* swallow */ }

        try
        {
            this._listener?.Close();
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] TCP on {Config.Port} stopped");

            // Close all active connections gracefully
            InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?
                                    .CloseAllConnections();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] ERROR closing listener socket: {ex.Message}");
        }
        finally
        {
            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
            _cts?.Dispose();
            _cts = null;
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