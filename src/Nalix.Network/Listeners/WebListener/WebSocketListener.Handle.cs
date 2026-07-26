// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Connections;

#pragma warning disable IDE0079
#pragma warning disable CA2213
#pragma warning disable CA1031

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
    #region Fields

    private IWorkerHandle? _processWorker;
    private Channel<IConnection>? _processChannel;

    #endregion Fields

    #region APIs

    /// <summary>
    /// Handles the close event of a connection.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The event arguments.</param>
    [DebuggerStepThrough]
    protected void HandleConnectionClose(object? sender, IConnectionEventArgs args)
    {
        if (args?.Connection == null)
        {
            return;
        }

        args.Connection.ConnectionClosed -= this.HandleConnectionClose;
        args.Connection.ConnectionClosed -= _limiter.OnConnectionClosed;
        args.Connection.MessageProcessed -= _protocol.PostProcessMessage;
        args.Connection.MessageProcessing -= _protocol.FrameProcessor.ProcessFrame;

        args.Connection.Dispose();
    }

    /// <summary>
    /// Processes a new connection.
    /// </summary>
    /// <param name="connection">The newly accepted connection.</param>
    [DebuggerStepThrough]
    protected void ProcessConnection(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            _protocol.OnAccept(connection);

            if (connection != null && !connection.IsDisposed)
            {
                // Register to the global connection pool/hub so that the server can broadcast
                // messages or force-close connections globally.
                _hub.RegisterConnection(connection);
                this.Metrics.RECORD_ACCEPTED();
            }
            else
            {
                this.Metrics.RECORD_REJECTED();
            }

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.WebSocketListenerBase:ProcessConnection", $"new-connection remote-endpoint={connection?.NetworkEndpoint}"));
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.Metrics.RECORD_ERROR();
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.LoopFaulted, new DiagnosticLog("NW.WebSocketListenerBase:ProcessConnection", $"process-error remote-endpoint={connection?.NetworkEndpoint}", ex));
            }
            connection?.Dispose();
        }
    }

    /// <summary>
    /// Asynchronously accepts connections from the HttpListener.
    /// </summary>
    /// <param name="ctx">The worker context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [DebuggerStepThrough]
    protected async Task AcceptConnectionsAsync(IWorkerContext ctx, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (_listener == null)
        {
            return;
        }

        int backoffMs = 50;

        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            ctx.Beat();
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);

                if (context.Request.IsWebSocketRequest)
                {
                    if (_forwardedConfig.Enabled &&
                        _forwardedConfig.RequireTrustedProxy &&
                        context.Request.RemoteEndPoint is IPEndPoint remoteEp &&
                        !_limiter.IsTrustedProxy(remoteEp))
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                        {
                            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.WebSocketListenerBase:AcceptConnectionsAsync", $"untrusted-proxy-rejected remote-endpoint={remoteEp}"));
                        }

                        this.Metrics.RECORD_LIMITER_REJECTION();
                        context.Response.StatusCode = 403; // Forbidden
                        context.Response.Close();
                        continue;
                    }

                    EndPoint realEndpoint = this.GET_REAL_ENDPOINT(context.Request);

                    // 1. Check Rate Limiter BEFORE accepting the WebSocket handshake.
                    // WHY: AcceptWebSocketAsync performs cryptographic operations (SHA1) and allocations.
                    // If we are under a DDoS attack or a client is spamming connections,
                    // we want to drop the request as early and as cheaply as possible with HTTP 429.
                    if (realEndpoint is not IPEndPoint ip || !_limiter.TryAccept(ip))
                    {
                        this.Metrics.RECORD_LIMITER_REJECTION();
                        context.Response.StatusCode = 429;
                        context.Response.Close();
                        continue;
                    }

                    // 2. Validate the Origin header to block Cross-Site WebSocket Hijacking (CSWSH).
                    // WHY here: after the rate limiter (so an origin-spoofing flood is still throttled)
                    // but before AcceptWebSocketAsync, so a rejected browser page never triggers the
                    // SHA1 handshake or any allocation. No-op when the allowlist is empty (legacy behavior).
                    if (!_config.IsOriginAllowed(context.Request.Headers["Origin"]))
                    {
                        this.Metrics.RECORD_REJECTED();
                        context.Response.StatusCode = 403; // Forbidden
                        context.Response.Close();
                        continue;
                    }

                    HttpListenerWebSocketContext wsContext;
                    try
                    {
                        TimeSpan keepAlive = _config.KeepAliveIntervalSeconds > 0
                            ? TimeSpan.FromSeconds(_config.KeepAliveIntervalSeconds)
                            : WebSocket.DefaultKeepAliveInterval;

                        wsContext = await context.AcceptWebSocketAsync(_config.SubProtocol, keepAlive).ConfigureAwait(false);
                    }
#pragma warning disable CA1031
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
#pragma warning restore CA1031
                    {
                        this.Metrics.RECORD_REJECTED();
                        try
                        {
                            context.Response.Close();
                        }
#pragma warning disable CA1031
                        catch
#pragma warning restore CA1031
                        {
                            // Ignore close failures on aborted clients.
                        }

                        continue;
                    }

#pragma warning disable CA2000
                    WebSocketConnection connection = new(wsContext.WebSocket, _protocol.OpCodeExtractor, realEndpoint);
#pragma warning restore CA2000

                    try
                    {
                        this.InitializeConnection(connection);

                        if (_processChannel != null)
                        {
                            if (!_processChannel.Writer.TryWrite(connection))
                            {
                                this.Metrics.RECORD_QUEUE_FULL_REJECTION();
                                connection.Disconnect("Queue full");
                            }
                        }
                        else
                        {
                            this.Metrics.RECORD_QUEUE_FULL_REJECTION();
                            connection.Disconnect("Queue completed");
                        }
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        this.Metrics.RECORD_ERROR();
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                        {
                            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketListenerBase:AcceptConnectionsAsync", "Failed to initialize connection", ex));
                        }
                        connection.Dispose();
                    }
                }
                else if (context.Request.HttpMethod == "GET" &&
                         string.Equals(context.Request.Url?.AbsolutePath, _config.HealthPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Health/uptime probe (Cloudflare Health Checks, UptimeRobot, LB health check).
                    // Returns 200 so monitors don't flag the server as down.
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
                else
                {
                    this.Metrics.RECORD_REJECTED();
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }

                backoffMs = 50;
                ctx.Advance(1, note: "accepted");
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                this.Metrics.RECORD_ERROR();
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketListenerBase:AcceptConnectionsAsync", "accept-loop-error", ex));
                }

                backoffMs = Math.Min(backoffMs * 2, 1000);
                await Task.Delay(backoffMs + Random.Shared.Next(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    #endregion APIs

    #region Private Methods

    private void START_PROCESS_CHANNEL(CancellationToken cancellationToken)
    {
        _processChannel = Channel.CreateBounded<IConnection>(
            new BoundedChannelOptions(_config.ProcessChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false,
            });

        _processWorker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
            name: $"{TaskNaming.Tags.Net}.{TaskNaming.Tags.WebSocket}.{TaskNaming.Tags.Accept}.{_port}",
            group: $"{TaskNaming.Tags.Net}/{TaskNaming.Tags.WebSocket}/{_port}",
            work: this.PROCESS_CHANNEL_LOOP_ASYNC,
            options: new WorkerOptions
            {
                Tag = TaskNaming.Tags.Net,
                RetainFor = TimeSpan.Zero,
                IdType = SnowflakeType.System,
                CancellationToken = cancellationToken,
                OSPriority = ThreadPriority.BelowNormal,
                ProcessorAffinity = _config.DispatchProcessorAffinity >= 0 ? _config.DispatchProcessorAffinity : null,
            });
    }

    private void STOP_PROCESS_CHANNEL()
    {
        _ = (_processChannel?.Writer.TryComplete());

        IWorkerHandle? worker = Interlocked.Exchange(ref _processWorker, null);
        if (worker != null)
        {
            worker.Dispose();

            int elapsed = 0;
            int timeout = _config.ProcessChannelDrainTimeout;
            while (worker.IsRunning && elapsed < timeout)
            {
                Thread.Sleep(10);
                elapsed += 10;
            }
        }
    }

    private async ValueTask PROCESS_CHANNEL_LOOP_ASYNC(IWorkerContext ctx, CancellationToken cancellationToken)
    {
        Channel<IConnection>? processChannel = _processChannel;
        if (processChannel is null)
        {
            return;
        }

        ChannelReader<IConnection> reader = processChannel.Reader;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ctx.Beat();

#pragma warning disable CA2000
                while (reader.TryRead(out IConnection? connection))
#pragma warning restore CA2000
                {
                    if (connection is null)
                    {
                        continue;
                    }

                    this.INVOKE_PROCESS(connection);
                    ctx.Advance(1);
                }

                if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketListenerBase:Internal", $"unhandled-error port={_port}", ex));
            }
        }
        finally
        {
            while (reader.TryRead(out IConnection? connection))
            {
                if (connection is null)
                {
                    continue;
                }

                this.INVOKE_PROCESS(connection);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void INVOKE_PROCESS(IConnection connection)
    {
        try
        {
            this.ProcessConnection(connection);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.HANDLE_PROCESS_ERROR(connection, ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HANDLE_PROCESS_ERROR(IConnection connection, Exception ex)
    {
        this.Metrics.RECORD_ERROR();
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
        {
            string remoteEndpoint = connection?.NetworkEndpoint?.ToString() ?? "<null>";
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketListenerBase:Internal", $"error remote remote-endpoint={remoteEndpoint}", ex));
            }
            ;
        }
        connection?.Disconnect();
    }

    /// <summary>
    /// Resolves the actual IP address of the client, even if the application is behind a reverse proxy.
    /// </summary>
    private EndPoint GET_REAL_ENDPOINT(HttpListenerRequest request)
    {
        if (!_forwardedConfig.Enabled)
        {
            return request.RemoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 0);
        }

        // 1. Check standard proxy headers.
        // Cloudflare uses CF-Connecting-IP. Nginx/HAProxy typically use X-Forwarded-For or X-Real-IP.
        string? forwardedFor = request.Headers["CF-Connecting-IP"] ?? request.Headers["X-Forwarded-For"] ?? request.Headers["X-Real-IP"];

        if (!string.IsNullOrEmpty(forwardedFor))
        {
            int commaIndex = forwardedFor.IndexOf(',', StringComparison.Ordinal);
            string ipString = commaIndex > 0 ? forwardedFor[..commaIndex].Trim() : forwardedFor.Trim();

            if (IPAddress.TryParse(ipString, out IPAddress? ip))
            {
                return new IPEndPoint(ip, request.RemoteEndPoint?.Port ?? 0);
            }
        }

        return request.RemoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeConnection(WebSocketConnection connection)
    {
        // Subscribe to lifecycle events.
        connection.ConnectionClosed += this.HandleConnectionClose;

        // WHY subscribe to _limiter.OnConnectionClosed:
        // When the connection closes, the limiter's active connection counter for this IP 
        // must be decremented so the client can connect again in the future.
        connection.ConnectionClosed += _limiter.OnConnectionClosed;

        // Keep post-process as you already have.
        // If your PostProcessMessage should run after app protocol, leaving it subscribed is OK.
        connection.MessageProcessed += _protocol.PostProcessMessage;

        // Wire the internal listener method to handle the shared pipeline before routing.
        connection.MessageProcessing += _protocol.FrameProcessor.ProcessFrame;

        if (_config.EnableTimeout)
        {
            // Register connection with TimingWheel to track idle timeout.
            // WHY TimingWheel instead of Timer per-connection:
            // - Timer per-connection: O(n) memory + GC pressure when there are thousands of connections.
            // - TimingWheel: O(1) tick, shared wheel for all connections -> much more efficient.
            _timing.Register(connection);
        }
    }

    #endregion Private Methods
}
