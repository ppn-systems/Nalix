// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal.Time;

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
    /// <inheritdoc/>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Activate(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.WebSocketListenerBase:Activate", $"activate-request port={_port}"));
        }

        if (!_lock.Wait(0, CancellationToken.None))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.WebSocketListenerBase:Activate", $"activate-skipped lock-busy port={_port}"));
            }
            return;
        }

        CancellationToken linkedToken = default;

        try
        {
            if ((ListenerState)Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.WebSocketListenerBase:Activate", $"ignored-activate state={this.State}"));
                }
                return;
            }

            _ = Interlocked.Exchange(ref _stopInitiated, 0);
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STARTING);

            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedToken = _cts.Token;

            _cancelReg = linkedToken.Register(static s =>
            {
                if (s is WebSocketListenerBase listener)
                {
                    listener.SCHEDULE_STOP();
                }
            }, this);

            bool needInit = _listener == null || !_listener.IsListening;
            if (needInit)
            {
                this.Initialize();
            }

            _ = Interlocked.Exchange(ref _state, (int)ListenerState.RUNNING);

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.WebSocketListenerBase:Activate", $"start protocol={_protocol} port={_port} path={_path}"));
            }

            if (_config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>().Activate(linkedToken);
            }

            if (_config.MaxParallel < 1)
            {
                throw new InternalErrorException("_config.MaxParallel must be at least 1.");
            }

            // Spawn N accept-worker async tasks, where N = MaxParallel.
            int workers = _config.MaxParallel;
            IWorkerHandle[] acceptWorkers = new IWorkerHandle[workers];
            for (int i = 0; i < workers; i++)
            {
                acceptWorkers[i] = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                    name: $"{TaskNaming.Tags.Net}.{TaskNaming.Tags.WebSocket}.{TaskNaming.Tags.Accept}.{i}",
                    group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.WebSocket}/{_port}",
                    work: async (ctx, ct) => await this.AcceptConnectionsAsync(ctx, ct).ConfigureAwait(false),
                    options: new WorkerOptions
                    {
                        Tag = TaskNaming.Tags.Net,
                        IdType = SnowflakeType.System,
                        CancellationToken = linkedToken,
                        RetainFor = TimeSpan.FromSeconds(30),
                    }
                );
            }
            _acceptWorkers = acceptWorkers;

            this.START_PROCESS_CHANNEL(linkedToken);
        }
        catch (OperationCanceledException)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.WebSocketListenerBase:Activate", $"cancel port={_port}"));
            }
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Critical))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Critical, new DiagnosticLog("NW.WebSocketListenerBase:Activate", $"critical-error port={_port}", ex));
            }
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
        finally
        {
            _ = _lock.Release();
        }
    }

    /// <inheritdoc/>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Deactivate(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _isDisposed) != 0 && this.State == ListenerState.STOPPED)
        {
            return;
        }

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.WebSocketListenerBase:Deactivate", $"deactivate-request port={_port}"));
        }

        int prev = Interlocked.CompareExchange(ref _state, (int)ListenerState.STOPPING, (int)ListenerState.RUNNING);

        if (prev != (int)ListenerState.RUNNING)
        {
            prev = Interlocked.CompareExchange(ref _state, (int)ListenerState.STOPPING, (int)ListenerState.STARTING);
            if (prev != (int)ListenerState.STARTING)
            {
                return;
            }
        }

        CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);
        try
        {
            try { _cancelReg.Dispose(); } catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
            try { cts?.Cancel(); } catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
            try
            {
                if (_listener != null && _listener.IsListening)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }

            _listener = null;
            this.STOP_PROCESS_CHANNEL();

            IWorkerHandle[]? acceptWorkers = Interlocked.Exchange(ref _acceptWorkers, null);
            if (acceptWorkers != null)
            {
                foreach (IWorkerHandle? worker in acceptWorkers)
                {
                    worker?.Dispose();
                }
            }

            if (_config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>().Deactivate(CancellationToken.None);
            }

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.WebSocketListenerBase:Deactivate", $"stop protocol={_protocol} port={_port}"));
            }
        }
        finally
        {
            try { cts?.Dispose(); } catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
            _cts = null;
            _ = Interlocked.Exchange(ref _state, (int)ListenerState.STOPPED);
        }
    }

    /// <inheritdoc/>
    [DebuggerStepThrough]
    protected virtual void Initialize()
    {
        if (_listener != null)
        {
            try { _listener.Close(); } catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
        }

        _listener = new HttpListener();

        string host = _config.Host;
        if (string.IsNullOrEmpty(host))
        {
            host = "*";
        }
        string prefix = $"http://{host}:{_port}{_path}";
        if (!prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        _listener.Prefixes.Add(prefix);
        _listener.Start();

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.WebSocketListenerBase:Initialize", $"listener-bound prefix={prefix}"));
        }
    }

    /// <inheritdoc/>
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual string GenerateReport()
    {
        StringBuilder sb = new(1024);
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] WebSocketListenerBase Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Port                : {_port}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Path                : {_path}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"StateWrapper        : {this.State}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MaxParallelAccepts  : {_config.MaxParallel}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Disposed            : {_isDisposed}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Metrics:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Accepted      : {this.Metrics.TotalAccepted}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Rejected      : {this.Metrics.TotalRejected}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  - Queue Full      : {this.Metrics.TotalQueueFullRejections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  - Limiter/Guard   : {this.Metrics.TotalLimiterRejections}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total Errors        : {this.Metrics.TotalErrors}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Accept Queue Depth  : {this.ProcessChannelCount}");
        _ = sb.AppendLine("--------------------------------------------");
        return sb.ToString();
    }

    /// <inheritdoc/>
    public virtual void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber("Port", _port);
        writer.WriteString("Path", _path);
        writer.WriteString(nameof(this.State), this.State.ToString());
        writer.WriteNumber("MaxParallelAccepts", _config.MaxParallel);
        writer.WriteBoolean("Disposed", _isDisposed != 0);

        writer.WriteStartObject(nameof(this.Metrics));
        writer.WriteNumber("TotalAccepted", this.Metrics.TotalAccepted);
        writer.WriteNumber("TotalRejected", this.Metrics.TotalRejected);
        writer.WriteNumber("QueueFullRejections", this.Metrics.TotalQueueFullRejections);
        writer.WriteNumber("LimiterRejections", this.Metrics.TotalLimiterRejections);
        writer.WriteNumber("TotalErrors", this.Metrics.TotalErrors);
        writer.WriteNumber("AcceptQueueDepth", this.ProcessChannelCount);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
