// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Contracts.Events;
using Nalix.Chat.Contracts.Packets;
using Nalix.Common.Exceptions;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Time;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.Chat.Client.Core.Networking;

/// <summary>
/// Owns TCP session lifecycle, request/response correlation, reconnect, and cipher rotation.
/// </summary>
public sealed class NetworkClient : INetworkClient
{
    private readonly TransportOptions _options;
    private static readonly RequestOptions s_requestOptions = RequestOptions.Default.WithTimeout(5_000);
    private readonly TcpSession _session;
    private readonly CompositeSubscription _subscriptions;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Lock _reconnectSync = new();

    private Task? _reconnectTask;
    private Timer? _cipherRotationTimer;
    private long _nextJoinRequestId;
    private long _nextMessageId;
    private int _cipherRotationCounter;
    private int _disconnectRequested;
    private int _disposed;
    private int _state = (int)ConnectionState.Disconnected;

    /// <summary>
    /// Initializes a new network client.
    /// </summary>
    public NetworkClient(TransportOptions? options = null)
    {
        _options = options ?? new TransportOptions();

        PacketRegistry registry = new(factory =>
        {
            _ = factory
                .RegisterPacket<JoinRoomRequest>()
                .RegisterPacket<JoinRoomResponse>()
                .RegisterPacket<ChatMessageRequest>()
                .RegisterPacket<ChatMessageAck>()
                .RegisterPacket<ChatMessageBroadcast>()
                .RegisterPacket<Control>()
                .RegisterPacket<Handshake>()
                .RegisterPacket<SessionResume>();
        });

        _session = new TcpSession(_options, registry);
        _subscriptions = new CompositeSubscription();

        _subscriptions.Add(_session.On<ChatMessageBroadcast>(this.OnMessageBroadcast));

        _session.OnConnected += this.HandleConnected;
        _session.OnDisconnected += this.HandleDisconnected;
        _session.OnError += this.HandleError;
    }

    /// <inheritdoc/>
    public ConnectionState ConnectionState => (ConnectionState)Volatile.Read(ref _state);

    /// <inheritdoc/>
    public CipherSuiteType ActiveCipher => _options.Algorithm;

    /// <inheritdoc/>
    public int CipherRotationCounter => Volatile.Read(ref _cipherRotationCounter);

    /// <inheritdoc/>
    public string SessionIdentifier => _options.SessionToken.ToString();

    /// <inheritdoc/>
    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<ChatMessageBroadcast>? MessageReceived;

    /// <inheritdoc/>
    public event EventHandler<Exception>? Error;

    /// <inheritdoc/>
    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(NetworkClient));

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_session.IsConnected)
            {
                this.SetState(ConnectionState.Connected);
                return;
            }

            _ = Interlocked.Exchange(ref _disconnectRequested, 0);
            this.SetState(ConnectionState.Connecting);

            _ = await _session.ConnectWithResumeAsync(ct: cancellationToken).ConfigureAwait(false);
            this.StartCipherRotationTimer();
            this.SetState(ConnectionState.Connected);
        }
        finally
        {
            _ = _lifecycleGate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
        {
            return;
        }

        _ = Interlocked.Exchange(ref _disconnectRequested, 1);
        this.StopCipherRotationTimer();

        await _session.DisconnectAsync().ConfigureAwait(false);

        this.SetState(ConnectionState.Disconnected);
    }

    /// <inheritdoc/>
    public async ValueTask<JoinRoomResponse> JoinRoomAsync(
        string roomId,
        string participantId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        this.EnsureConnected();

        long requestId = Interlocked.Increment(ref _nextJoinRequestId);
        JoinRoomRequest request = new()
        {
            RoomId = roomId,
            ParticipantId = participantId,
            DisplayName = displayName,
            ClientRequestId = requestId
        };

        return await _session.RequestAsync<JoinRoomResponse>(
            request,
            options: s_requestOptions,
            predicate: response => response.ClientRequestId == requestId,
            ct: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ChatMessageAck> SendMessageAsync(
        string roomId,
        string participantId,
        string message,
        CancellationToken cancellationToken = default)
    {
        this.EnsureConnected();

        long clientMessageId = Interlocked.Increment(ref _nextMessageId);
        ChatMessageRequest request = new()
        {
            RoomId = roomId,
            ParticipantId = participantId,
            ClientMessageId = clientMessageId,
            Message = message,
            ClientTimestampUnixMs = Clock.UnixMillisecondsNow()
        };

        return await _session.RequestAsync<ChatMessageAck>(
            request,
            options: s_requestOptions,
            predicate: ack => ack.ClientMessageId == clientMessageId,
            ct: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<double> PingAsync(CancellationToken cancellationToken = default)
    {
        this.EnsureConnected();
        return await _session.PingAsync(ct: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<double> SyncClockAsync(CancellationToken cancellationToken = default)
    {
        this.EnsureConnected();
        _ = await _session.SyncTimeAsync(ct: cancellationToken).ConfigureAwait(false);
        return Clock.CurrentErrorEstimateMs();
    }

    /// <inheritdoc/>
    public async ValueTask RotateCipherAsync(CancellationToken cancellationToken = default)
    {
        this.EnsureConnected();

        CipherSuiteType next = _options.Algorithm == CipherSuiteType.Chacha20Poly1305
            ? CipherSuiteType.Salsa20Poly1305
            : CipherSuiteType.Chacha20Poly1305;

        await _session.UpdateCipherAsync(next, ct: cancellationToken).ConfigureAwait(false);
        _ = Interlocked.Increment(ref _cipherRotationCounter);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _disposeCts.Cancel();

        try
        {
            await this.DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        this.StopCipherRotationTimer();

        _subscriptions.Dispose();

        _session.OnConnected -= this.HandleConnected;
        _session.OnDisconnected -= this.HandleDisconnected;
        _session.OnError -= this.HandleError;

        _session.Dispose();
        _disposeCts.Dispose();
        _lifecycleGate.Dispose();
    }

    private void OnMessageBroadcast(ChatMessageBroadcast broadcast) => MessageReceived?.Invoke(this, broadcast);

    private void HandleConnected(object? sender, EventArgs args) => this.SetState(ConnectionState.Connected);

    private void HandleDisconnected(object? sender, Exception exception)
    {
        this.StopCipherRotationTimer();

        if (Interlocked.CompareExchange(ref _disconnectRequested, 0, 0) == 1)
        {
            this.SetState(ConnectionState.Disconnected);
            return;
        }

        if (!_options.ReconnectEnabled || Volatile.Read(ref _disposed) == 1)
        {
            this.SetState(ConnectionState.Disconnected);
            return;
        }

        this.SetState(ConnectionState.Reconnecting);
        this.StartReconnectLoop();
    }

    private void HandleError(object? sender, Exception exception) => Error?.Invoke(this, exception);

    private void StartReconnectLoop()
    {
        lock (_reconnectSync)
        {
            if (_reconnectTask is { IsCompleted: false })
            {
                return;
            }

            _reconnectTask = Task.Run(async () => await this.ReconnectLoopAsync(_disposeCts.Token).ConfigureAwait(false), CancellationToken.None);
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        int attempt = 0;

        while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _disposed) == 0)
        {
            if (_options.ReconnectMaxAttempts > 0 && attempt >= _options.ReconnectMaxAttempts)
            {
                this.SetState(ConnectionState.Faulted);
                return;
            }

            attempt++;

            try
            {
                _ = await _session.ConnectWithResumeAsync(ct: cancellationToken).ConfigureAwait(false);
                this.StartCipherRotationTimer();
                this.SetState(ConnectionState.Connected);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }

            int exponent = attempt > 20 ? 20 : attempt;
            int delay = _options.ReconnectBaseDelayMillis * (1 << exponent);
            if (delay > _options.ReconnectMaxDelayMillis)
            {
                delay = _options.ReconnectMaxDelayMillis;
            }

            if (delay < 0)
            {
                delay = _options.ReconnectMaxDelayMillis;
            }

            if (delay == 0)
            {
                delay = 250;
            }

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void StartCipherRotationTimer()
    {
        if (_cipherRotationTimer is not null)
        {
            return;
        }

        _cipherRotationTimer = new Timer(
            static state => ((NetworkClient)state!).OnCipherRotationTimerTick(),
            this,
            dueTime: TimeSpan.FromMinutes(30),
            period: TimeSpan.FromMinutes(30));
    }

    private void StopCipherRotationTimer()
    {
        Timer? timer = Interlocked.Exchange(ref _cipherRotationTimer, null);
        timer?.Dispose();
    }

    private void OnCipherRotationTimerTick()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (!_session.IsConnected || Volatile.Read(ref _disposed) == 1)
                {
                    return;
                }

                await this.RotateCipherAsync(_disposeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal/shutdown when cancellation is requested.
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }, CancellationToken.None);
    }

    private void SetState(ConnectionState state)
    {
        ConnectionState previous = (ConnectionState)Interlocked.Exchange(ref _state, (int)state);
        if (previous == state)
        {
            return;
        }

        ConnectionStateChanged?.Invoke(this, state);
    }

    private void EnsureConnected()
    {
        if (!_session.IsConnected)
        {
            throw new InvalidOperationException("NetworkClient is not connected.");
        }
    }
}
