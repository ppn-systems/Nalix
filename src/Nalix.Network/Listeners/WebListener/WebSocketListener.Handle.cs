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
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Connections;

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
    private Channel<IConnection>? _processChannel;
#pragma warning disable CA2213 // Disposable fields should be disposed
    private IWorkerHandle? _processWorker;
#pragma warning restore CA2213 // Disposable fields should be disposed

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
            InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelWorker(worker.Id);

            int elapsed = 0;
            int timeout = _config.ProcessChannelDrainTimeout;
            while (worker.IsRunning && elapsed < timeout)
            {
                Thread.Sleep(10);
                elapsed += 10;
            }

            worker.Dispose();
        }
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
            this.Protocol.OnAccept(connection);

            if (connection != null && !connection.IsDisposed)
            {
                _hub.RegisterConnection(connection);
            }

            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Trace))
            {
                this.Logger.LogTrace($"[NW.{nameof(WebSocketListenerBase)}:{nameof(ProcessConnection)}] new={connection?.NetworkEndpoint}");
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
            {
                this.Logger.LogError(ex, $"[NW.{nameof(WebSocketListenerBase)}:{nameof(ProcessConnection)}] process-error={connection?.NetworkEndpoint}");
            }
            connection?.Dispose();
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
            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
            {
                this.Logger.LogError(ex, $"[NW.{nameof(WebSocketListenerBase)}:{nameof(PROCESS_CHANNEL_LOOP_ASYNC)}] unhandled-error port={_port}");
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void INVOKE_PROCESS(IConnection connection)
    {
        try
        {
            this.ProcessConnection(connection);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
            {
                this.Logger.LogError(ex, $"[NW.{nameof(WebSocketListenerBase)}:{nameof(INVOKE_PROCESS)}] error remote={connection?.NetworkEndpoint.ToString() ?? "<null>"} port={_port}");
            }
            connection?.Disconnect();
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
                    HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(_config.SubProtocol).ConfigureAwait(false);

#pragma warning disable CA2000
                    WebSocketConnection connection = new(wsContext.WebSocket, context.Request.RemoteEndPoint, this.Logger);
#pragma warning restore CA2000

                    connection.OnCloseEvent += this.HandleConnectionClose;
                    connection.OnProcessEvent += this.ProcessFrame;
                    connection.OnPostProcessEvent += this.Protocol.PostProcessMessage;

                    if (_config.EnableTimeout)
                    {
                        _timing.Register(connection);
                    }

                    if (_processChannel != null)
                    {
                        if (!_processChannel.Writer.TryWrite(connection))
                        {
                            connection.Disconnect();
                        }
                    }
                    else
                    {
                        connection.Disconnect();
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
        args.Connection.OnProcessEvent -= this.ProcessFrame;
        args.Connection.OnPostProcessEvent -= this.Protocol.PostProcessMessage;

        args.Connection.Dispose();
    }

    /// <summary>
    /// Processes an incoming frame from the connection.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The event arguments.</param>
    public abstract void ProcessFrame(object? sender, IConnectEventArgs args);
}
