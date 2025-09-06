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

        // Create/refresh CTS early
        _cts?.Dispose();
        _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationToken = _cts.Token;
        System.Threading.CancellationToken linkedToken = _cts.Token;

        // Ensure socket closes on cancel
        using System.Threading.CancellationTokenRegistration registration = linkedToken.Register(() =>
        {
            try
            {
                _listener?.Close();
            }
            catch { }
        });

        // IMPORTANT: Acquire the lifecycle lock BEFORE transitioning state
        await _lock.WaitAsync(linkedToken).ConfigureAwait(false);
        try
        {
            // If someone already started (or is running), exit early
            if ((ListenerState)System.Threading.Volatile.Read(ref _state) != ListenerState.Stopped)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(TcpListenerBase)}] Activate called but state = {State}");
                return;
            }

            // Stopped -> Starting -> Running (inside lock so we won't get stuck in Starting)
            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Starting);

            // (Re)init listener safely
            System.Boolean needInit = false;
            try
            {
                needInit = _listener is null || !_listener.IsBound || _listener.SafeHandle.IsInvalid;
            }
            catch { needInit = true; }
            if (needInit)
            {
                Initialize();
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TcpListenerBase)}] Starting listener");

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Running);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] {_protocol} listening on port {Config.Port}");

            System.Collections.Generic.List<System.Threading.Tasks.Task> tasks = new(1 + Config.MaxParallel)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                                        .StartTickLoopAsync(linkedToken)
            };

            for (System.Int32 i = 0; i < Config.MaxParallel; i++)
            {
                System.Threading.Tasks.Task accept = AcceptConnectionsAsync(linkedToken);

                // Log faults without binding the continuation to the cancel token
                _ = accept.ContinueWith(t =>
                {
                    if (t.IsFaulted && !linkedToken.IsCancellationRequested)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[{nameof(TcpListenerBase)}] Accept task failed: " +
                                                       $"{t.Exception?.GetBaseException().Message}");
                    }
                },
                System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted |
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);

                tasks.Add(accept);
            }

            await System.Threading.Tasks.Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}] on {Config.Port} stopped by cancellation");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}] Could not start {_protocol} on port {Config.Port}", ex);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Fatal($"[{nameof(TcpListenerBase)}] Critical error in listener on port {Config.Port}", ex);
        }
        finally
        {
            try
            {
                _cts?.Cancel();
                _listener?.Close();
                // Do NOT pass an external token here to avoid OCE during shutdown
                await System.Threading.Tasks.Task.Delay(200, System.Threading.CancellationToken.None)
                                                 .ConfigureAwait(false);

                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().StopTicking();
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TcpListenerBase)}] Listener during shutdown: {ex.Message}");
            }
            finally
            {
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
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
        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        logger?.Debug($"[{nameof(TcpListenerBase)}] Deactivate requested on port {Config.Port}");

        // Try Running->Stopping; if not, try Starting->Stopping
        System.Int32 prev = System.Threading.Interlocked.CompareExchange(ref _state,
            (System.Int32)ListenerState.Stopping, (System.Int32)ListenerState.Running);

        if (prev != (System.Int32)ListenerState.Running)
        {
            prev = System.Threading.Interlocked.CompareExchange(ref _state,
                (System.Int32)ListenerState.Stopping, (System.Int32)ListenerState.Starting);

            if (prev != (System.Int32)ListenerState.Starting)
            {
                logger?.Warn($"[{nameof(TcpListenerBase)}] Deactivate called but state = {State}");
                return;
            }
        }

        var cts = System.Threading.Interlocked.Exchange(ref _cts, null);
        try
        {
            try { cts?.Cancel(); logger?.Debug($"[{nameof(TcpListenerBase)}] CTS cancelled for {Config.Port}"); } catch { }
            try { _listener?.Close(); logger?.Debug($"[{nameof(TcpListenerBase)}] Listener socket closed {Config.Port}"); } catch { }

            InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?.CloseAllConnections();
            logger?.Info($"[{nameof(TcpListenerBase)}] TCP on {Config.Port} stopped");
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