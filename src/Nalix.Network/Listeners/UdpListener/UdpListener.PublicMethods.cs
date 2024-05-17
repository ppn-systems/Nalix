// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Framework.Time;
using Nalix.Network.Abstractions;
using Nalix.Network.Timing;

namespace Nalix.Network.Listeners.Udp;

/// <summary>
/// Provides a base implementation for a UDP network listener, supporting asynchronous listening,
/// protocol processing, and time synchronization. Inherit from this class to implement custom UDP listeners.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Port={Config?.Port}, Running={_isRunning}")]
public abstract partial class UdpListenerBase : IListener
{
    /// <summary>
    /// Starts listening for incoming UDP datagrams and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    [System.Diagnostics.DebuggerStepThrough]
    public void Activate(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (this._isRunning)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(UdpListenerBase)}:{nameof(Activate)}] already-running");
            return;
        }

        if (this._udpClient == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(UdpListenerBase)}:{nameof(Activate)}] init port={_port}");
            this.Initialize();
        }

        try
        {
            this._isRunning = true;
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(UdpListenerBase)}:{nameof(Activate)}] start protocol={_protocol} port={_port}");

            this._cts?.Dispose();
            this._cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this._cancellationToken = this._cts.Token;

            _lock.Wait(_cancellationToken);

            try
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[{nameof(UdpListenerBase)}] listening port={_port}");

                System.Threading.Tasks.Task receiveTask = this.ReceiveDatagramsAsync(this._cancellationToken);
                _ = receiveTask.ConfigureAwait(false);

                _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?.StartWorker(
                   name: $"udp.proc.{_port}",
                   group: $"net/udp/{_port}",
                   work: async (_, ct) => await ReceiveDatagramsAsync(ct),
                   options: new WorkerOptions
                   {
                       Tag = "udp",
                       CancellationToken = _cancellationToken
                   });
            }
            finally
            {
                _ = _lock.Release();
            }
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(UdpListenerBase)}:{nameof(Activate)}] cancel port={_port}");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Fatal($"[{nameof(UdpListenerBase)}:{nameof(Activate)}] bind-fail port={_port}", ex);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Fatal($"[{nameof(UdpListenerBase)}:{nameof(Activate)}] critical port={_port}", ex);
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
                        _ = System.Threading.Tasks.Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(UdpListenerBase)}:{nameof(Activate)}] shutdown-error", ex);
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
    [System.Diagnostics.DebuggerStepThrough]
    public void Deactivate(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        this._cts?.Cancel();

        try
        {
            if (this._isRunning)
            {
                this._udpClient?.Close();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[{nameof(UdpListenerBase)}:{nameof(Deactivate)}] stopped port={_port}");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[{nameof(UdpListenerBase)}:{nameof(Deactivate)}] stop-error", ex);
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
    [System.Diagnostics.DebuggerStepThrough]
    public virtual void SynchronizeTime(System.Int64 milliseconds)
    {
        // Record last sync and drift vs local clock
        System.Int64 now = Clock.UnixMillisecondsNow();
        _lastSyncUnixMs = milliseconds;
        _lastDriftMs = now - milliseconds;

        // Hook for derived listeners (optional override)
        this.OnTimeSynchronized(milliseconds, now, _lastDriftMs);
    }

    /// <summary>
    /// Called when the listener synchronizes its time with the server.
    /// </summary>
    /// <param name="serverMs">The current server time in milliseconds since the Unix epoch.</param>
    /// <param name="localMs">The local time in milliseconds since the Unix epoch.</param>
    /// <param name="driftMs">The calculated drift in milliseconds between server and local time.</param>
    [System.Diagnostics.DebuggerStepThrough]
    protected virtual void OnTimeSynchronized(System.Int64 serverMs, System.Int64 localMs, System.Int64 driftMs)
    {
        // No-op by default
    }

    /// <summary>
    /// Determines whether the incoming packet is authenticated.
    /// Default returns true (i.e., trusted). Override in derived class.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    protected abstract System.Boolean IsAuthenticated(
        IConnection connection, in System.Net.Sockets.UdpReceiveResult result);

    /// <summary>
    /// Generates a human-readable diagnostic report of the current listener status.
    /// </summary>
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new(512);

        // IsListening wraps _isRunning:contentReference[oaicite:10]{index=10}
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] UdpListener Status:");
        _ = sb.AppendLine($"Port: {_port}");
        _ = sb.AppendLine($"IsListening: {this.IsListening}");
        _ = sb.AppendLine($"IsDisposed: {_isDisposed}");
        _ = sb.AppendLine($"Protocol: {EllipseLeft(_protocol?.GetType().FullName ?? _protocol?.GetType().Name ?? "<null>", 23)}");
        _ = sb.AppendLine();

        // Socket configuration (static Config):contentReference[oaicite:11]{index=11}
        _ = sb.AppendLine("Socket Config:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine($"NoDelay: {Config.NoDelay}");
        _ = sb.AppendLine($"ReuseAddress: {Config.ReuseAddress}");
        _ = sb.AppendLine($"KeepAlive: {Config.KeepAlive}");
        _ = sb.AppendLine($"BufferSize: {Config.BufferSize}");
        _ = sb.AppendLine();

        // Worker info: spawn/group + concurrency = 8 in ReceiveDatagramsAsync:contentReference[oaicite:12]{index=12}
        _ = sb.AppendLine("Worker:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine($"Group: udp.port.{_port}");
        _ = sb.AppendLine("Configured GroupConcurrencyLimit: 8");
        _ = sb.AppendLine();

        // Time sync
        // property getter used by base:contentReference[oaicite:13]{index=13}
        System.Boolean timeSyncEnabled = InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                                                        .IsTimeSyncEnabled;
        _ = sb.AppendLine("Time Sync:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine($"Enabled: {timeSyncEnabled}");
        _ = sb.AppendLine($"LastSyncUnixMs: {_lastSyncUnixMs}");
        _ = sb.AppendLine($"LastDriftMs(local-now - server): {_lastDriftMs}");
        _ = sb.AppendLine();

        // Traffic stats
        System.Int64 rxPackets = System.Threading.Interlocked.Read(ref _rxPackets);
        System.Int64 rxBytes = System.Threading.Interlocked.Read(ref _rxBytes);
        System.Int64 dropShort = System.Threading.Interlocked.Read(ref _dropShort);
        System.Int64 dropUnauth = System.Threading.Interlocked.Read(ref _dropUnauth);
        System.Int64 dropUnknown = System.Threading.Interlocked.Read(ref _dropUnknown);

        _ = sb.AppendLine("Traffic:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine($"ReceivedPackets: {rxPackets}");
        _ = sb.AppendLine($"ReceivedBytes: {rxBytes}");
        _ = sb.AppendLine($"Dropped: short={dropShort}, unauth={dropUnauth}, unknown={dropUnknown}");
        _ = sb.AppendLine();

        // Errors summary (bind/recv/shutdown) from Activate/Receive handling:contentReference[oaicite:14]{index=14}:contentReference[oaicite:15]{index=15}
        System.Int64 recvErrors = System.Threading.Interlocked.Read(ref _recvErrors);

        _ = sb.AppendLine("Errors:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine($"ReceiveErrors: {recvErrors}");
        _ = sb.AppendLine();

        // Live objects
        _ = sb.AppendLine("Runtime:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine($"UdpClient: {(_udpClient is null ? "<null>" : "OK")}");
        _ = sb.AppendLine($"CTS: {(_cts is null ? "<null>" : "OK")}");
        _ = sb.AppendLine();

        return sb.ToString();
    }
}