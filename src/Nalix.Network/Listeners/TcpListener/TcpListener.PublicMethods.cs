// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Tasks;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Framework.Time;
using Nalix.Network.Connection;
using Nalix.Network.Internal.Net;
using Nalix.Network.Timing;

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
    public void Activate(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (Config.MaxParallel < 1)
        {
            throw new System.InvalidOperationException(
                $"[{nameof(TcpListenerBase)}:{nameof(Activate)}] Config.MaxParallel must be at least 1.");
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}:{nameof(Activate)}] activate-request port={_port}");

        _lock.Wait(System.Threading.CancellationToken.None);

        System.Threading.CancellationToken linkedToken = default;

        try
        {
            if ((ListenerState)System.Threading.Volatile.Read(ref _state) != ListenerState.Stopped)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(TcpListenerBase)}:{nameof(Activate)}] ignored-activate state={State}");

                return;
            }

            System.Threading.Interlocked.Exchange(ref _stopInitiated, 0);
            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Starting);

            _cts?.Dispose();
            _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationToken = _cts.Token;

            linkedToken = _cts.Token;
            _ = linkedToken.Register(static s => ((TcpListenerBase)s!).ScheduleStop(), this);

            System.Boolean needInit;
            try
            {
                needInit = _listener?.IsBound != true || _listener.SafeHandle.IsInvalid;
            }
            catch
            {
                needInit = true;
            }
            if (needInit)
            {
                Initialize();
            }

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Running);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}:{nameof(Activate)}] start protocol={_protocol} port={_port}");

            if (Config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Activate(linkedToken);
            }

            _acceptWorkerIds.Clear();

            for (System.Int32 i = 0; i < Config.MaxParallel; i++)
            {
                IWorkerHandle h = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
                    name: NetTaskNames.TcpAcceptWorker(_port, i),
                    group: NetTaskNames.TcpGroup(_port),
                    work: async (_, ct) => await AcceptConnectionsAsync(ct).ConfigureAwait(false),
                    options: new WorkerOptions
                    {
                        CancellationToken = linkedToken,
                        RetainFor = System.TimeSpan.FromSeconds(30),
                        IdType = IdentifierType.System,
                        Tag = NetTaskNames.Segments.Net
                    }
                );

                _acceptWorkerIds.Add(h.Id);
            }

            return;
        }
        catch (System.OperationCanceledException)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}:{nameof(Activate)}] cancel port={_port}");

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TcpListenerBase)}: {nameof(Activate)} ] start-failed port= {_port}", ex);

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Fatal($"[{nameof(TcpListenerBase)}:{nameof(Activate)}] critical-error port={_port}", ex);

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
        }
        finally
        {
            _ = _lock.Release();
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Deactivate(System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}:{nameof(Deactivate)}] deactivate-request port={_port}");

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
                                        .Warn($"[{nameof(TcpListenerBase)}:{nameof(Deactivate)}] ignored-deactivate state={State}");
                return;
            }
        }

        if (Config.EnableTimeout)
        {
            InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                    .Deactivate(System.Threading.CancellationToken.None);
        }

        var cts = System.Threading.Interlocked.Exchange(ref _cts, null);
        try
        {
            try { cts?.Cancel(); } catch { }
            try { _listener?.Close(); } catch { }

            _listener = null;

            _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                        .CancelGroup(NetTaskNames.TcpGroup(_port));

            _ = (InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                         .CancelGroup(NetTaskNames.TcpProcessGroup(_port)));

            InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?
                                    .CloseAllConnections();

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(TcpListenerBase)}:{nameof(Deactivate)}] stop protocol={_protocol} port={_port}");
        }
        finally
        {
            try
            {
                cts?.Dispose();
            }
            catch { }
            _cts = null;
            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.Stopped);
        }
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    [System.Diagnostics.DebuggerStepThrough]
    public virtual void SynchronizeTime(System.Int64 milliseconds)
    { }

    /// <summary>
    /// Generates a diagnostic report of the TCP listener state and metrics.
    /// </summary>
    /// <returns>A formatted string report.</returns>
    public virtual System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new();
        System.Threading.ThreadPool.GetMinThreads(out System.Int32 minWorker, out System.Int32 minIocp);

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TcpListenerBase Status:");
        _ = sb.AppendLine($"Port                : {_port}");
        _ = sb.AppendLine($"State               : {State}");
        _ = sb.AppendLine($"Disposed            : {_isDisposed}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine($"EnableTimeout    : {Config.EnableTimeout}");
        _ = sb.AppendLine($"MaxParallelAccepts  : {Config.MaxParallel}");
        _ = sb.AppendLine($"BufferSize          : {Config.BufferSize}");
        _ = sb.AppendLine($"KeepAlive           : {Config.KeepAlive}");
        _ = sb.AppendLine($"ReuseAddress        : {Config.ReuseAddress}");
        _ = sb.AppendLine($"EnableIPv6          : {Config.EnableIPv6}");
        _ = sb.AppendLine($"Backlog             : {Config.Backlog}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Protocol:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine($"BoundProtocol       : {_protocol.ToString() ?? "-"}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Connections:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine($"ActiveConnections   : {InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?.Count}");
        _ = sb.AppendLine($"LimiterEnabled      : {true}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Threading:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine($"ThreadPool MinWorker: {minWorker}");
        _ = sb.AppendLine($"ThreadPool MinIOCP  : {minIocp}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("TimeSync:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine($"IsTimeSyncEnabled   : {IsTimeSyncEnabled}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("--------------------------------------------");
        return sb.ToString();
    }
}