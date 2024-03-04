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
    public async System.Threading.Tasks.Task ActivateAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (Config.MaxParallel < 1)
        {
            throw new System.InvalidOperationException($"[{nameof(TcpListenerBase)}] Config.MaxParallel must be at least 1.");
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                        .Debug($"[{nameof(TcpListenerBase)}] activate-request port={_port}");

        // Acquire lifecycle lock WITHOUT external token
        await _lock.WaitAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);

        System.Boolean started = false;
        System.Threading.CancellationToken linkedToken = default;
        System.Threading.CancellationTokenRegistration registration = default;

        try
        {
            if ((ListenerState)System.Threading.Volatile.Read(ref _state) != ListenerState.Stopped)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(TcpListenerBase)}] ignored-activate state={State}");
                return;
            }

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Starting);

            _cts?.Dispose();
            _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationToken = _cts.Token;
            linkedToken = _cts.Token;

            registration = linkedToken.Register(() =>
            {
                try { _listener?.Close(); } catch { }
            });

            System.Boolean needInit;
            try
            {
                needInit = _listener is null || !_listener.IsBound || _listener.SafeHandle.IsInvalid;
            }
            catch { needInit = true; }
            if (needInit)
            {
                Initialize();
            }

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Running);
            started = true;

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] start protocol={_protocol} port={_port}");

            if (Config.TimeoutOnConnect)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Activate();
            }

            var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>(1 + Config.MaxParallel)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                                        .StartTickLoopAsync(linkedToken)
            };

            for (System.Int32 i = 0; i < Config.MaxParallel; i++)
            {
                var accept = AcceptConnectionsAsync(linkedToken);

                _ = accept.ContinueWith(t =>
                    {
                        if (t.IsFaulted && !linkedToken.IsCancellationRequested)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Error($"[{nameof(TcpListenerBase)}] " +
                                                           $"accept-task-failed msg={t.Exception?.GetBaseException().Message}");
                        }
                    },
                    System.Threading.CancellationToken.None,
                    System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted |
                    System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                    System.Threading.Tasks.TaskScheduler.Default
                );

                tasks.Add(accept);
            }

            await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] cancel port={_port}");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] start-failed port={_port} ex={ex.Message}", ex);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Fatal($"[{nameof(TcpListenerBase)}] critical-error port={_port}", ex);
        }
        finally
        {
            try
            {
                if (started)
                {
                    try { _cts?.Cancel(); } catch { }
                    try { _listener?.Close(); } catch { }
                    _listener = null;

                    await System.Threading.Tasks.Task.Delay(200, System.Threading.CancellationToken.None).ConfigureAwait(false);

                    InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().StopTicking();
                }
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] shutdown-error port={_port} ex={ex.Message}");
            }
            finally
            {
                try { registration.Dispose(); } catch { }
                try { _cts?.Dispose(); } catch { }
                _cts = null;

                if (started)
                {
                    _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
                }

                _ = _lock.Release();
            }
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public async System.Threading.Tasks.Task DeactivateAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}] deactivate-request port={_port}");

        // Try Running->Stopping; if not, try Starting->Stopping
        System.Int32 prev = System.Threading.Interlocked.CompareExchange(ref _state,
            (System.Int32)ListenerState.Stopping, (System.Int32)ListenerState.Running);

        if (prev != (System.Int32)ListenerState.Running)
        {
            prev = System.Threading.Interlocked.CompareExchange(ref _state,
                (System.Int32)ListenerState.Stopping, (System.Int32)ListenerState.Starting);

            if (prev != (System.Int32)ListenerState.Starting)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(TcpListenerBase)}] ignored-deactivate state={State}");
                return;
            }
        }

        if (Config.TimeoutOnConnect)
        {
            InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                    .Deactivate();
        }

        var cts = System.Threading.Interlocked.Exchange(ref _cts, null);
        try
        {
            try { cts?.Cancel(); } catch { }
            try { _listener?.Close(); } catch { }

            _listener = null;

            InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?.CloseAllConnections();
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] TCP on {Config.Port} stopped");
        }
        finally
        {
            try { cts?.Dispose(); } catch { }
            _cts = null;
            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
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