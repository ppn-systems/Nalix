// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Concurrency;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Time;

namespace Nalix.Network.Listeners.Tcp;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
[DebuggerDisplay("Port={_port}, StateWrapper={StateWrapper}")]
public abstract partial class TcpListenerBase
{
    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    /// <exception cref="InternalErrorException"></exception>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        if (s_config.MaxParallel < 1)
        {
            throw new InternalErrorException("s_config.MaxParallel must be at least 1.");
        }

        s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] activate-request port={_port}");

        // Acquire mutex — SemaphoreSlim.Wait() with CancellationToken.None:
        // WHY None (not cancel): Activate() must complete even if the external token is cancelled.
        // If you use cancellationToken here → cancel before lock acquire → inconsistent state.
        _lock.Wait(CancellationToken.None);

        CancellationToken linkedToken = default;

        try
        {
            // State check inside lock → avoid race condition "double Activate".
            if ((ListenerState)Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] ignored-activate state={this.State}");

                return;
            }

            _ = Interlocked.Exchange(ref _stopInitiated, 0);
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STARTING);

            // Create a new linked CTS with an external token.
            // WHY linked: When caller cancel → _cts also cancel → all workers stop.
            // WHY dispose old CTS first: Avoid leaks if Activate is called again after Deactivate.
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationToken = _cts.Token;

            linkedToken = _cts.Token;
            _cancelReg = linkedToken.Register(static s =>
            {
                if (s is TcpListenerBase listener)
                {
                    listener.SCHEDULE_STOP();
                }
            }, this);

            bool needInit;
            try
            {
                Socket? listener = _listener;
                needInit = listener is null || !listener.IsBound || listener.SafeHandle.IsInvalid;
            }
            catch
            {
                needInit = true;
            }

            if (needInit)
            {
                this.Initialize();
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.RUNNING);

            s_logger?.Info($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] start protocol={_protocol} port={_port}");

            if (s_config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Activate(linkedToken);
            }

            _acceptWorkerIds.Clear();

            // Spawn N accept-worker async tasks (N = MaxParallel).
            // WHY N workers instead of 1: On multi-core, N workers run in parallel → increment throughput accept.
            // Each independent worker await CreateConnectionAsync → N connections can be accepted concurrently.
            for (int i = 0; i < s_config.MaxParallel; i++)
            {
                IWorkerHandle h = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                    name: $"{TaskNaming.Tags.Tcp}.{TaskNaming.Tags.Accept}.{i}",
                    group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Tcp}/{_port}",
                    work: async (ctx, ct) => await this.AcceptConnectionsAsync(ctx, ct).ConfigureAwait(false),
                    options: new WorkerOptions
                    {
                        Tag = TaskNaming.Tags.Net,
                        IdType = SnowflakeType.System,
                        CancellationToken = linkedToken,
                        RetainFor = TimeSpan.FromSeconds(30),
                    }
                );

                _acceptWorkerIds.Add(h.Id);
            }

            this.START_PROCESS_CHANNEL(linkedToken);
        }
        catch (OperationCanceledException)
        {
            s_logger?.Info($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] cancel port={_port}");

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
        catch (SocketException ex)
        {
            s_logger?.Error($"[NW.{nameof(TcpListenerBase)}: {nameof(Activate)} ] start-failed port= {_port}", ex);

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
        catch (Exception ex)
        {
            s_logger?.Critical($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] critical-error port={_port}", ex);

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
        finally
        {
            _ = _lock.Release();
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        // Skip throwing if already disposed; just return calmly or let ListenerState check handle it.
        if (Volatile.Read(ref _isDisposed) != 0 && this.State == ListenerState.STOPPED)
        {
            return;
        }

        s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] deactivate-request port={_port}");

        // Try Running->Stopping; if not, try Starting->Stopping
        int prev = Interlocked.CompareExchange(ref _state,
            (int)ListenerState.STOPPING, (int)ListenerState.RUNNING);

        if (prev != (int)ListenerState.RUNNING)
        {
            prev = Interlocked.CompareExchange(ref _state,
                (int)ListenerState.STOPPING, (int)ListenerState.STARTING);

            if (prev != (int)ListenerState.STARTING)
            {
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] ignored-deactivate state={this.State}");

                return;
            }
        }

        CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);
        try
        {
            try { _cancelReg.Dispose(); } catch { }
            try { cts?.Cancel(); } catch { }
            try { _listener?.Close(); } catch { }

            _listener = null;

            this.STOP_PROCESS_CHANNEL();

            _ = (InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                         .CancelGroup($"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Tcp}/{_port}"));

            InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?
                                    .CloseAllConnections();

            if (s_config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Deactivate(CancellationToken.None);
            }

            s_logger?.Info($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] stop protocol={_protocol} port={_port}");
        }
        finally
        {
            try
            {
                cts?.Dispose();
            }
            catch { }

            _cts = null;
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
    }

    #region IReportable Implementation

    /// <summary>
    /// Generates a diagnostic report of the TCP listener state and metrics.
    /// </summary>
    /// <returns>A formatted string report.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual string GenerateReport()
    {
        StringBuilder sb = new(2048);
        ThreadPool.GetMinThreads(out int minWorker, out int minIocp);

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TcpListenerBase Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Port                : {_port}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"StateWrapper        : {this.State}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Disposed            : {_isDisposed}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"EnableTimeout       : {s_config.EnableTimeout}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MaxParallelAccepts  : {s_config.MaxParallel}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"BufferSize          : {s_config.BufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"KeepAlive           : {s_config.KeepAlive}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReuseAddress        : {s_config.ReuseAddress}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"EnableIPv6          : {s_config.EnableIPv6}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Backlog             : {s_config.Backlog}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Metrics:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Accepted      : {this.Metrics.TotalAccepted}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Rejected      : {this.Metrics.TotalRejected}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Errors        : {this.Metrics.TotalErrors}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Protocol:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"BoundProtocol       : {_protocol.ToString() ?? "-"}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Connections:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ActiveConnections   : {InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?.Count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"LimiterEnabled      : {true}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Threading:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ThreadPool MinWorker: {minWorker}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ThreadPool MinIOCP  : {minIocp}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("--------------------------------------------");
        return sb.ToString();
    }

    /// <summary>
    /// Generates diagnostic data as key-value pairs describing the current TCP listener state and metrics.
    /// </summary>
    /// <returns>A dictionary containing the report data.</returns>
    public virtual IDictionary<string, object> GenerateReportData()
    {
        ThreadPool.GetMinThreads(out int minWorker, out int minIocp);

        Dictionary<string, object> data = new(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["Port"] = _port,
            ["State"] = this.State,
            ["Disposed"] = _isDisposed,
            ["Configuration"] = new Dictionary<string, object>
            {
                ["EnableTimeout"] = s_config.EnableTimeout,
                ["MaxParallelAccepts"] = s_config.MaxParallel,
                ["BufferSize"] = s_config.BufferSize,
                ["KeepAlive"] = s_config.KeepAlive,
                ["ReuseAddress"] = s_config.ReuseAddress,
                ["EnableIPv6"] = s_config.EnableIPv6,
                ["Backlog"] = s_config.Backlog
            },
            ["Metrics"] = new Dictionary<string, object>
            {
                ["TotalAccepted"] = this.Metrics.TotalAccepted,
                ["TotalRejected"] = this.Metrics.TotalRejected,
                ["TotalErrors"] = this.Metrics.TotalErrors
            },
            ["Protocol"] = new Dictionary<string, object>
            {
                ["BoundProtocol"] = _protocol?.ToString() ?? "-"
            },
            ["Connections"] = new Dictionary<string, object>
            {
                ["ActiveConnections"] = InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?.Count ?? 0,
                ["LimiterEnabled"] = true
            },
            ["Threading"] = new Dictionary<string, object>
            {
                ["ThreadPoolMinWorker"] = minWorker,
                ["ThreadPoolMinIOCP"] = minIocp
            }
        };

        return data;
    }

    #endregion IReportable Implementation
}
