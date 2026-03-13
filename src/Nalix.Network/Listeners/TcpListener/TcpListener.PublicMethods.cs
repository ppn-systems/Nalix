// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Identity.Enums;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Connections;
using Nalix.Network.Internal;
using Nalix.Network.Timekeeping;

namespace Nalix.Network.Listeners.Tcp;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Port={_port}, StateWrapper={StateWrapper}")]
public abstract partial class TcpListenerBase
{
    /// <summary>
    /// Generates a diagnostic report of the TCP listener state and metrics.
    /// </summary>
    /// <returns>A formatted string report.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public virtual System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new(512);
        System.Threading.ThreadPool.GetMinThreads(out System.Int32 minWorker, out System.Int32 minIocp);

        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TcpListenerBase Status:");
        _ = sb.AppendLine($"Port                : {_port}");
        _ = sb.AppendLine($"StateWrapper        : {State}");
        _ = sb.AppendLine($"Disposed            : {_isDisposed}");
        _ = sb.AppendLine();

        _ = sb.AppendLine("Configuration:");
        _ = sb.AppendLine("--------------------------------------------");
        _ = sb.AppendLine($"EnableTimeout       : {_config.EnableTimeout}");
        _ = sb.AppendLine($"MaxParallelAccepts  : {_config.MaxParallel}");
        _ = sb.AppendLine($"BufferSize          : {_config.BufferSize}");
        _ = sb.AppendLine($"KeepAlive           : {_config.KeepAlive}");
        _ = sb.AppendLine($"ReuseAddress        : {_config.ReuseAddress}");
        _ = sb.AppendLine($"EnableIPv6          : {_config.EnableIPv6}");
        _ = sb.AppendLine($"Backlog             : {_config.Backlog}");
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

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC),
    /// as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual void SynchronizeTime([System.Diagnostics.CodeAnalysis.NotNull] System.Int64 milliseconds) { }

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Activate([System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _isDisposed) != 0, this);

        if (_config.MaxParallel < 1)
        {
            throw new System.InvalidOperationException("_config.MaxParallel must be at least 1.");
        }

        s_logger.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] activate-request port={_port}");

        _lock.Wait(System.Threading.CancellationToken.None);

        System.Threading.CancellationToken linkedToken = default;

        try
        {
            if ((ListenerState)System.Threading.Volatile.Read(ref _state) != ListenerState.STOPPED)
            {
                s_logger.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] ignored-activate state={State}");

                return;
            }

            _ = System.Threading.Interlocked.Exchange(ref _stopInitiated, 0);
            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.STARTING);

            _cts?.Dispose();
            _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellationToken = _cts.Token;

            linkedToken = _cts.Token;
            _cancelReg = linkedToken.Register(static s => ((TcpListenerBase)s!).SCHEDULE_STOP(), this);

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

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.RUNNING);

            s_logger.Info($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] start protocol={_protocol} port={_port}");

            if (_config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Activate(linkedToken);
            }

            _acceptWorkerIds.Clear();

            for (System.Int32 i = 0; i < _config.MaxParallel; i++)
            {
                IWorkerHandle h = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                    name: $"{NetTaskNames.Tcp}.{TaskNaming.Tags.Accept}.{i}",
                    group: $"{NetTaskNames.Net}/{NetTaskNames.Tcp}/{_port}",
                    work: async (ctx, ct) => await AcceptConnectionsAsync(ctx, ct).ConfigureAwait(false),
                    options: new WorkerOptions
                    {
                        CancellationToken = linkedToken,
                        RetainFor = System.TimeSpan.FromSeconds(30),
                        IdType = SnowflakeType.System,
                        Tag = NetTaskNames.Net
                    }
                );

                _acceptWorkerIds.Add(h.Id);
            }

            return;
        }
        catch (System.OperationCanceledException)
        {
            s_logger.Info($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] cancel port={_port}");

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.STOPPED);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            s_logger.Error($"[NW.{nameof(TcpListenerBase)}: {nameof(Activate)} ] start-failed port= {_port}", ex);

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.STOPPED);
        }
        catch (System.Exception ex)
        {
            s_logger.Fatal($"[NW.{nameof(TcpListenerBase)}:{nameof(Activate)}] critical-error port={_port}", ex);

            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.STOPPED);
        }
        finally
        {
            _ = _lock.Release();
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Deactivate([System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref this._isDisposed) != 0, this);

        s_logger.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] deactivate-request port={_port}");

        // Try Running->Stopping; if not, try Starting->Stopping
        System.Int32 prev = System.Threading.Interlocked.CompareExchange(ref _state,
            (System.Int32)ListenerState.STOPPING, (System.Int32)ListenerState.RUNNING);

        if (prev != (System.Int32)ListenerState.RUNNING)
        {
            prev = System.Threading.Interlocked.CompareExchange(ref _state,
                (System.Int32)ListenerState.STOPPING, (System.Int32)ListenerState.STARTING);

            if (prev != (System.Int32)ListenerState.STARTING)
            {
                s_logger.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] ignored-deactivate state={State}");

                return;
            }
        }

        if (_config.EnableTimeout)
        {
            InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                    .Deactivate(System.Threading.CancellationToken.None);
        }

        System.Threading.CancellationTokenSource cts = System.Threading.Interlocked.Exchange(ref _cts, null);
        try
        {
            try { _cancelReg.Dispose(); } catch { }
            try { cts?.Cancel(); } catch { }
            try { _listener?.Close(); } catch { }

            _listener = null;

            _ = (InstanceManager.Instance.GetExistingInstance<TaskManager>()?
                                         .CancelGroup($"{NetTaskNames.Net}/{NetTaskNames.Tcp}/{_port}"));

            InstanceManager.Instance.GetExistingInstance<ConnectionHub>()?
                                    .CloseAllConnections();

            s_logger.Info($"[NW.{nameof(TcpListenerBase)}:{nameof(Deactivate)}] stop protocol={_protocol} port={_port}");
        }
        finally
        {
            try
            {
                cts?.Dispose();
            }
            catch { }
            _cts = null;
            _ = System.Threading.Interlocked.Exchange(ref _state, (System.Int32)ListenerState.STOPPED);
        }
    }
}
