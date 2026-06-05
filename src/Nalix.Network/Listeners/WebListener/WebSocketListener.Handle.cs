// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Diagnostics;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Connections;

#pragma warning disable IDE0079
#pragma warning disable CA2213

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
    protected void HandleConnectionClose(object? sender, IConnectEventArgs args)
    {
        if (args?.Connection == null)
        {
            return;
        }

        args.Connection.OnCloseEvent -= this.HandleConnectionClose;
        args.Connection.OnCloseEvent -= _limiter.OnConnectionClosed;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage;
        args.Connection.OnProcessEvent -= _protocol.FrameProcessor.ProcessFrame;

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
            }

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.WebSocketListenerBase:ProcessConnection", $"new-connection remote-endpoint={connection?.NetworkEndpoint}"));
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.LoopFaulted))
            {
                DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.LoopFaulted, new DiagnosticLog("NW.WebSocketListenerBase:ProcessConnection", $"process-error remote-endpoint={connection?.NetworkEndpoint}", ex));
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
                            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.WebSocketListenerBase:AcceptConnectionsAsync", $"untrusted-proxy-rejected remote-endpoint={remoteEp}"));
        };
                        }

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
                        context.Response.StatusCode = 429;
                        context.Response.Close();
                        continue;
                    }

                    // 2. Negotiate the WebSocket handshake.

                    HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(_config.SubProtocol).ConfigureAwait(false);

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
                                connection.Disconnect("Queue full");
                            }
                        }
                        else
                        {
                            connection.Disconnect("Queue completed");
                        }
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                        {
                            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketListenerBase:AcceptConnectionsAsync", "Failed to initialize connection", ex));
                        }
                        connection.Dispose();
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
                ctx.Advance(1, note: "accepted");
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
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
                OSPriority = ThreadPriority.BelowNormal,
                Tag = TaskNaming.Tags.Net,
                IdType = SnowflakeType.System,
                RetainFor = TimeSpan.Zero,
                CancellationToken = cancellationToken
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
                DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketListenerBase:Internal", $"unhandled-error port={_port}", ex));
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
        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
        {
            string remoteEndpoint = connection?.NetworkEndpoint?.ToString() ?? "<null>";
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.WebSocketListenerBase:Internal", $"error remote remote-endpoint={remoteEndpoint}", ex));
        };
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
        connection.OnCloseEvent += this.HandleConnectionClose;

        // WHY subscribe to _limiter.OnConnectionClosed:
        // When the connection closes, the limiter's active connection counter for this IP 
        // must be decremented so the client can connect again in the future.
        connection.OnCloseEvent += _limiter.OnConnectionClosed;

        // Keep post-process as you already have.
        // If your PostProcessMessage should run after app protocol, leaving it subscribed is OK.
        connection.OnPostProcessEvent += _protocol.PostProcessMessage;

        // Wire the internal listener method to handle the shared pipeline before routing.
        connection.OnProcessEvent += _protocol.FrameProcessor.ProcessFrame;

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
