// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Listeners.Udp;

/// <summary>
/// Provides a base implementation for a UDP network listener, supporting asynchronous listening,
/// protocol processing, and time synchronization. Inherit from this class to implement custom UDP listeners.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerDisplay("Port={s_config?.Port}, Running={_isRunning}")]
[Obsolete("Udp listener is not ready for use and is currently unsupported. This feature is in development and may change or be removed in future releases.", error: false)]
public abstract partial class UdpListenerBase : IListener
{
    /// <summary>
    /// Starts listening for incoming UDP datagrams and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        if (_isRunning)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] already-running");
            return;
        }

        if (_udpClient == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] init port={_port}");
            this.Initialize();
        }

        bool started = false;

        try
        {
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationToken = _cts.Token;

            _lock.Wait(_cancellationToken);

            try
            {
                _isRunning = true;
                started = true;

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] start protocol={_protocol} port={_port}");
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] listening port={_port}");

                _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?.ScheduleWorker(
                    name: $"{TaskNaming.Tags.Udp}.{TaskNaming.Tags.Process}",              // "udp.proc"
                    group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Udp}/{_port}",            // "net/udp/port"
                    work: async (_, ct) => await this.ReceiveDatagramsAsync(ct).ConfigureAwait(false),
                    options: new WorkerOptions
                    {
                        Tag = TaskNaming.Tags.Udp,
                        IdType = SnowflakeType.System,
                        CancellationToken = _cancellationToken,
                        GroupConcurrencyLimit = s_config.MaxGroupConcurrency
                    });
            }
            finally
            {
                _ = _lock.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (started)
            {
                _isRunning = false;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] cancel port={_port}");
            _cts?.Dispose();
            _cts = null;
        }
        catch (SocketException ex)
        {
            if (started)
            {
                _isRunning = false;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Critical($"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] bind-fail port={_port}", ex);
            _cts?.Dispose();
            _cts = null;
        }
        catch (Exception ex)
        {
            if (started)
            {
                _isRunning = false;
            }

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Critical($"[NW.{nameof(UdpListenerBase)}:{nameof(Activate)}] critical port={_port}", ex);
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Stops the listener from receiving further UDP datagrams.
    /// </summary>
    /// <param name="cancellationToken">A token that may be used by derived implementations during shutdown.</param>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        _cts?.Cancel();

        try
        {
            _udpClient?.Close();
            _udpClient = null;

            if (_isRunning)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] stopped port={_port}");
            }
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[NW.{nameof(UdpListenerBase)}:{nameof(Deactivate)}] stop-error", ex);
        }
        finally
        {
            _isRunning = false;
            _cts?.Dispose();
            _cts = null;
            _cancellationToken = default;
        }
    }

    /// <summary>
    /// Determines whether the incoming packet is authenticated.
    /// Default returns true (i.e., trusted). Override in derived class.
    /// </summary>
    /// <param name="connection">The owning connection.</param>
    /// <param name="result">The received UDP result.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected abstract bool IsAuthenticated(IConnection connection, in UdpReceiveResult result);

    #region IReportable Implementation

    /// <summary>
    /// Generates a human-readable diagnostic report of the current listener status.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        StringBuilder sb = new(512);

        // IsListening wraps _isRunning:contentReference[oaicite:10]{index=10}
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] UdpListener Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Port: {_port}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"IsListening: {this.IsListening}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"IsDisposed: {_isDisposed}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Protocol: {EllipseLeft(_protocol?.GetType().FullName ?? _protocol?.GetType().Name ?? "<null>", 23)}");
        _ = sb.AppendLine();

        // Socket configuration (static Config):contentReference[oaicite:11]{index=11}
        _ = sb.AppendLine("Socket s_config:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"NoDelay: {s_config.NoDelay}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReuseAddress: {s_config.ReuseAddress}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"KeepAlive: {s_config.KeepAlive}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"BufferSize: {s_config.BufferSize}");
        _ = sb.AppendLine();

        // Worker info: spawn/group + concurrency = 8 in ReceiveDatagramsAsync:contentReference[oaicite:12]{index=12}
        _ = sb.AppendLine("Worker:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Group: udp.port.{_port}");
        _ = sb.AppendLine("Configured GroupConcurrencyLimit: 8");
        _ = sb.AppendLine();

        // Traffic stats
        long rxPackets = Interlocked.Read(ref _rxPackets);
        long rxBytes = Interlocked.Read(ref _rxBytes);
        long dropShort = Interlocked.Read(ref _dropShort);
        long dropUnauth = Interlocked.Read(ref _dropUnauth);
        long dropUnknown = Interlocked.Read(ref _dropUnknown);

        _ = sb.AppendLine("Traffic:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceivedPackets: {rxPackets}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceivedBytes: {rxBytes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Dropped: short={dropShort}, unauth={dropUnauth}, unknown={dropUnknown}");
        _ = sb.AppendLine();

        // Errors summary (bind/recv/shutdown) from Activate/Receive handling:contentReference[oaicite:14]{index=14}:contentReference[oaicite:15]{index=15}
        long recvErrors = Interlocked.Read(ref _recvErrors);

        _ = sb.AppendLine("Errors:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReceiveErrors: {recvErrors}");
        _ = sb.AppendLine();

        // Live objects
        _ = sb.AppendLine("Runtime:");
        _ = sb.AppendLine("------------------------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"UdpClient: {(_udpClient is null ? "<null>" : "OK")}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CTS: {(_cts is null ? "<null>" : "OK")}");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates status report data as key-value pairs describing the current listener state.
    /// </summary>
    /// <returns>A dictionary containing the report data.</returns>
    public IDictionary<string, object> GetReportData()
    {
        Dictionary<string, object> data = new(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["Port"] = _port,
            ["IsListening"] = this.IsListening,
            ["IsDisposed"] = _isDisposed,
            ["ProtocolType"] = _protocol?.GetType().FullName ?? _protocol?.GetType().Name ?? "<null>",

            ["Config"] = new Dictionary<string, object>
            {
                ["NoDelay"] = s_config.NoDelay,
                ["ReuseAddress"] = s_config.ReuseAddress,
                ["KeepAlive"] = s_config.KeepAlive,
                ["BufferSize"] = s_config.BufferSize
            },

            ["Worker"] = new Dictionary<string, object>
            {
                ["Group"] = $"udp.port.{_port}",
                ["ConfiguredGroupConcurrencyLimit"] = 8
            },

            ["Traffic"] = new Dictionary<string, object>
            {
                ["ReceivedPackets"] = Interlocked.Read(ref _rxPackets),
                ["ReceivedBytes"] = Interlocked.Read(ref _rxBytes),
                ["DroppedShort"] = Interlocked.Read(ref _dropShort),
                ["DroppedUnauth"] = Interlocked.Read(ref _dropUnauth),
                ["DroppedUnknown"] = Interlocked.Read(ref _dropUnknown)
            },

            ["Errors"] = new Dictionary<string, object>
            {
                ["ReceiveErrors"] = Interlocked.Read(ref _recvErrors)
            },

            ["Runtime"] = new Dictionary<string, object>
            {
                ["UdpClient"] = _udpClient is null ? "<null>" : "OK",
                ["CTS"] = _cts is null ? "<null>" : "OK"
            }
        };

        return data;
    }

    #endregion IReportable Implementation
}
