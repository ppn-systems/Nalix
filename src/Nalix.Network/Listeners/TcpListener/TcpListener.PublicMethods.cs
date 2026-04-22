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
using Nalix.Network.Internal.Time;

#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA2254 // Template should be a static expression

namespace Nalix.Network.Listeners.Tcp;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// It owns the listener lifecycle, the accept workers, and the shutdown flow
/// for a concrete TCP listener implementation.
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

        if (_config.MaxParallel < 1)
        {
            throw new InternalErrorException("s_connectionLimitOptions.MaxParallel must be at least 1.");
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] activate-request port={_port}");
        }

        // Avoid blocking lifecycle transitions behind a concurrent caller.
        if (!_lock.Wait(0, CancellationToken.None))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] " +
                    $"activate-skipped lock-busy port={_port}");
            }
            return;
        }

        CancellationToken linkedToken = default;

        try
        {
            // Check state while holding the lock so two concurrent Activate calls
            // cannot both observe STOPPED and initialize twice.
            if ((ListenerState)Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] ignored-activate state={this.State}");
                }

                return;
            }

            _ = Interlocked.Exchange(ref _stopInitiated, 0);
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STARTING);

            // Create a linked CTS so cancellation from the caller propagates to
            // every worker, timeout job, and background accept loop.
            // Disposing the previous CTS first avoids leaking registrations when
            // Activate/Deactivate cycles happen repeatedly.
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
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                needInit = true;
            }

            if (needInit)
            {
                this.Initialize();
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.RUNNING);

            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] start protocol={_protocol} port={_port}");
            }

            if (_config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Activate(linkedToken);
            }

            _acceptWorkerIds.Clear();

            // Spawn N accept-worker async tasks, where N = MaxParallel.
            // Multiple workers let the listener accept several connections in
            // parallel instead of serializing every accept behind one loop.
            for (int i = 0; i < _config.MaxParallel; i++)
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
            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] cancel port={_port}");
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
        catch (SocketException ex)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, $"[NW.{nameof(TcpListenerBase)}: {nameof(Activate)} ] start-failed port= {_port}");
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(ex, $"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] critical-error port={_port}");
            }

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
    /// <param name="cancellationToken">A token that may be used by derived implementations during shutdown.</param>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        // If the listener is already disposed and fully stopped, there is nothing
        // left to deactivate.
        if (Volatile.Read(ref _isDisposed) != 0 && this.State == ListenerState.STOPPED)
        {
            return;
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] deactivate-request port={_port}");
        }

        // Try RUNNING -> STOPPING first; if that fails, allow STARTING -> STOPPING
        // so shutdown works even while activation is still in progress.
        int prev = Interlocked.CompareExchange(ref _state,
            (int)ListenerState.STOPPING, (int)ListenerState.RUNNING);

        if (prev != (int)ListenerState.RUNNING)
        {
            prev = Interlocked.CompareExchange(ref _state,
                (int)ListenerState.STOPPING, (int)ListenerState.STARTING);

            if (prev != (int)ListenerState.STARTING)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] ignored-deactivate state={this.State}");
                }

                return;
            }
        }

        CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);
        try
        {
            try
            {
                _cancelReg.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cancel-reg-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex,
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cancel-reg-dispose-failed port={_port}");
                }
            }

            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException ex)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-cancel-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex,
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-cancel-failed port={_port}");
                }
            }

            try
            {
                _listener?.Close();
            }
            catch (ObjectDisposedException ex)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"listener-close-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex,
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"listener-close-failed port={_port}");
                }
            }

            _listener = null;

            this.STOP_PROCESS_CHANNEL();

            _ = (InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                         .CancelGroup($"{TaskNaming.Tags.Net}/{TaskNaming.Tags.Tcp}/{_port}"));

            if (_config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Deactivate(CancellationToken.None);
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] stop protocol={_protocol} port={_port}");
            }
        }
        finally
        {
            try
            {
                cts?.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-dispose-ignored port={_port} reason={ex.GetType().Name}");
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex,
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] " +
                        $"cts-dispose-failed port={_port}");
                }
            }

            _cts = null;
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
    }

    #region IReportable Implementation

    /// <summary>
    /// Generates a diagnostic report of the TCP listener state and metrics.
    /// This is a human-readable snapshot intended for troubleshooting and ops
    /// tooling, not a stable serialization format.
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
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"EnableTimeout       : {_config.EnableTimeout}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MaxParallelAccepts  : {_config.MaxParallel}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"BufferSize          : {_config.BufferSize}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"KeepAlive           : {_config.KeepAlive}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ReuseAddress        : {_config.ReuseAddress}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"EnableIPv6          : {_config.EnableIPv6}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Backlog             : {_config.Backlog}");
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
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"ActiveConnections   : {_hub.Count}");
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
    /// Generates diagnostic data as key-value pairs describing the current TCP
    /// listener state and metrics.
    /// This shape is easier for automation and structured logging to consume.
    /// </summary>
    /// <returns>A dictionary containing the report data.</returns>
    public virtual IDictionary<string, object> GetReportData()
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
                ["EnableTimeout"] = _config.EnableTimeout,
                ["MaxParallelAccepts"] = _config.MaxParallel,
                ["BufferSize"] = _config.BufferSize,
                ["KeepAlive"] = _config.KeepAlive,
                ["ReuseAddress"] = _config.ReuseAddress,
                ["EnableIPv6"] = _config.EnableIPv6,
                ["Backlog"] = _config.Backlog
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
                ["ActiveConnections"] = _hub.Count,
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
