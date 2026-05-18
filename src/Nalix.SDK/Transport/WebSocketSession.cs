// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Environment.Memory;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Internal.Web;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a WebSocket transport session built on <see cref="WsFrameReader"/> and <see cref="WsFrameSender"/>.
/// </summary>
public class WebSocketSession : TransportSession
{
    #region Fields

    private readonly WsFrameSender _sender;
    private readonly WsFrameReader _reader;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

#pragma warning disable CA2213
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _loopCts;
#pragma warning restore CA2213

    private int _disposed;

    #endregion Fields

    #region Properties

    /// <inheritdoc/>
    public override TransportOptions Options { get; }

    /// <inheritdoc/>
    public override bool IsConnected => _socket?.State == WebSocketState.Open && Volatile.Read(ref _disposed) == 0;

    #endregion Properties

    #region Events

    /// <inheritdoc/>
    public override event EventHandler? OnConnected;

    /// <inheritdoc/>
    public override event EventHandler<Exception>? OnDisconnected;

    /// <inheritdoc/>
    public override event EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public override event EventHandler<Exception>? OnError;

    /// <summary>Occurs when a complete frame is received and decoded asynchronously.</summary>
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageAsync;

    #endregion Events

    #region Constructor

    /// <summary>Initializes a new instance of the <see cref="WebSocketSession"/> class.</summary>
    /// <param name="options">The transport options for this session.</param>
    public WebSocketSession(TransportOptions options)
    {
        this.Options = options ?? throw new ArgumentNullException(nameof(options));

        _sender = new WsFrameSender(() => _socket!, options, this.HandleError);
        _reader = new WsFrameReader(() => _socket!, options, this.HandleReceiveMessage, this.HandleError);
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(WebSocketSession));

        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            string effectiveHost = string.IsNullOrWhiteSpace(host) ? this.Options.Address : host;
            ushort effectivePort = port ?? this.Options.Port;

            if (this.IsConnected)
            {
                await this.DisconnectInternalAsync().ConfigureAwait(false);
            }

            _socket = new ClientWebSocket();

            if (!string.IsNullOrWhiteSpace(this.Options.WebSocketSubProtocol))
            {
                _socket.Options.AddSubProtocol(this.Options.WebSocketSubProtocol);
            }

            string scheme = this.Options.WebSocketUseTls ? "wss" : "ws";
            string path = NormalizeWebSocketPath(this.Options.WebSocketPath);
            Uri uri = new($"{scheme}://{effectiveHost}:{effectivePort}{path}");

            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (this.Options.ConnectTimeoutMillis > 0)
            {
                connectCts.CancelAfter(TimeSpan.FromMilliseconds(this.Options.ConnectTimeoutMillis));
            }

            await _socket.ConnectAsync(uri, connectCts.Token).ConfigureAwait(false);
            this.OnConnected?.Invoke(this, EventArgs.Empty);

            _loopCts = new CancellationTokenSource();

            _ = Task.Factory.StartNew(() => _reader.ReceiveLoopAsync(_loopCts.Token),
                _loopCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            await this.DisconnectInternalAsync().ConfigureAwait(false);
            this.OnError?.Invoke(this, ex);
            throw new NetworkException($"WebSocket Connection failed: {ex.Message}", ex);
        }
        finally
        {
            _ = _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public override async Task DisconnectAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await this.DisconnectInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _connectionLock.Release();
        }
    }

    private async Task DisconnectInternalAsync()
    {
        CancellationTokenSource? loopCts = Interlocked.Exchange(ref _loopCts, null);
        ClientWebSocket? socket = Interlocked.Exchange(ref _socket, null);

        try
        {
#pragma warning disable CA1849
            loopCts?.Cancel();
#pragma warning restore CA1849
        }
        catch (ObjectDisposedException ex)
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                this.OnError?.Invoke(this, ex);
            }
        }
        finally
        {
            loopCts?.Dispose();
        }

        if (socket is not null)
        {
            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived || socket.State == WebSocketState.CloseSent)
                {
                    using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (Volatile.Read(ref _disposed) == 0)
                {
                    this.OnError?.Invoke(this, ex);
                }
            }

            socket.Dispose();
            this.OnDisconnected?.Invoke(this, new NetworkException("The WebSocket session was disconnected."));
        }
    }

    /// <summary>
    /// Sends raw binary data synchronously.
    /// </summary>
    public void Send(ReadOnlySpan<byte> data, bool encrypt = true) => _sender.Send(data, encrypt);

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, CancellationToken ct = default)
        => await this.SendAsync(packet, this.Options.EncryptionEnabled, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet is IPacketHeader h)
        {
            h.Flags = (h.Flags & ~PacketFlags.UNRELIABLE) | PacketFlags.RELIABLE;
        }

        BufferLease lease = BufferLease.Rent(packet.Length);
        lease.CommitLength(packet.Serialize(lease.SpanFull));
        bool sent = await _sender.SendAsync(lease, encrypt, ct).ConfigureAwait(false);

        if (!sent)
        {
            throw new NetworkException("Failed to send WebSocket packet: the frame was not delivered to the socket.");
        }
    }

    /// <inheritdoc/>
    public override async Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
        => await _sender.SendAsync(payload, encrypt, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing || Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = this.DisconnectInternalAsync();
        _sender.Dispose();
        _reader.Dispose();
        _connectionLock.Dispose();
    }

    #endregion APIs

    #region Private

    private static string NormalizeWebSocketPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        string normalized = path.Trim();
        return normalized[0] == '/' ? normalized : "/" + normalized;
    }

    private void HandleError(Exception ex)
    {
        this.OnError?.Invoke(this, ex);
        _ = this.DisconnectAsync();
    }

    private void HandleReceiveMessage(IBufferLease lease)
    {
        try
        {
            this.OnMessageReceived?.Invoke(this, lease);

            if (this.OnMessageAsync is { } asyncHandler)
            {
                lease.Retain();

                Task dispatchTask;
                try
                {
                    dispatchTask = asyncHandler(lease.Memory);
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    lease.Dispose();
                    this.OnError?.Invoke(this, ex);
                    return;
                }

                if (dispatchTask.IsCompletedSuccessfully)
                {
                    lease.Dispose();
                }
                else
                {
                    _ = dispatchTask.ContinueWith(static (task, state) =>
                    {
                        if (state is not Tuple<WebSocketSession, IBufferLease> payload)
                        {
                            return;
                        }

                        WebSocketSession self = payload.Item1;
                        IBufferLease retained = payload.Item2;
                        try
                        {
                            if (task.Exception?.GetBaseException() is Exception ex)
                            {
                                self.OnError?.Invoke(self, ex);
                            }
                        }
                        finally
                        {
                            retained.Dispose();
                        }
                    }, Tuple.Create(this, lease), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.OnError?.Invoke(this, ex);
        }
    }

    #endregion Private
}
