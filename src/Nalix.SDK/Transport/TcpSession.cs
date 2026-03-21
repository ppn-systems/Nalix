// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Tasks;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport;

/// <summary>
/// Represents a reliable TCP client session with automatic reconnection,
/// heartbeat support, and bandwidth monitoring.
/// </summary>
/// <remarks>
/// This class extends <see cref="BaseTcpSession"/> and delegates
/// framing, sending, and receiving logic to internal helpers.
/// </remarks>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class TcpSession : BaseTcpSession
{
    #region Constants and Static Fields

    /// <summary>
    /// Gets the size of the packet header in bytes.
    /// </summary>
    public const System.Byte HeaderSize = 2;

    /// <summary>
    /// Gets the global packet registry used for packet resolution.
    /// </summary>
    internal static readonly IPacketRegistry Catalog;

    /// <summary>
    /// Initializes static members of the <see cref="TcpSession"/> class.
    /// </summary>
    static TcpSession()
    {
        Catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
            ?? throw new System.InvalidOperationException("IPacketRegistry instance not found.");

        BufferConfig bufferConfig = ConfigurationManager.Instance.Get<BufferConfig>();
        bufferConfig.TotalBuffers = 32;
        bufferConfig.EnableMemoryTrimming = true;
        bufferConfig.TrimIntervalMinutes = 2;
        bufferConfig.DeepTrimIntervalMinutes = 10;
        bufferConfig.EnableAnalytics = false;
        bufferConfig.AdaptiveGrowthFactor = 1.25;
        bufferConfig.MaxMemoryPercentage = 0.05;
        bufferConfig.SecureClear = false;
        bufferConfig.EnableQueueCompaction = false;
        bufferConfig.AutoTuneOperationThreshold = 32;
        bufferConfig.FallbackToArrayPool = true;
        bufferConfig.ExpandThresholdPercent = 0.20;
        bufferConfig.ShrinkThresholdPercent = 0.60;
        bufferConfig.MinimumIncrease = 1;
        bufferConfig.MaxBufferIncreaseLimit = 16;
        bufferConfig.BufferAllocations = "256,0.25; 512,0.30; 1024,0.45";
        bufferConfig.MaxMemoryBytes = 0;
        bufferConfig.Validate();
    }

    #endregion

    #region Fields

    private IWorkerHandle? _receiveHandle;
    private System.String? _host;
    private System.UInt16? _port;
    private System.Int32 _reconnecting = 0;
    private System.Int32 _hasEverConnected = 0;
    internal System.Int64 _bytesSent = 0;
    internal System.Int64 _bytesReceived = 0;
    internal System.Int64? _lastSampleTick = 0;
    internal System.Int64 _sendCounterForInterval = 0;
    internal System.Int64 _receiveCounterForInterval = 0;
    internal System.Int64 _lastSendBps = 0;
    internal System.Int64 _lastReceiveBps = 0;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the total number of bytes sent.
    /// </summary>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total number of bytes received.
    /// </summary>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    /// <summary>
    /// Gets the current send rate in bytes per second.
    /// </summary>
    public System.Int64 SendBytesPerSecond => System.Threading.Interlocked.Read(ref _lastSendBps);

    /// <summary>
    /// Gets the current receive rate in bytes per second.
    /// </summary>
    public System.Int64 ReceiveBytesPerSecond => System.Threading.Interlocked.Read(ref _lastReceiveBps);

    #endregion

    /// <summary>
    /// Occurs when the client successfully reconnects.
    /// </summary>
    public event System.EventHandler<System.Int32>? OnReconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpSession"/> class.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when required configuration or dependencies cannot be loaded.
    /// </exception>
    public TcpSession() : base()
    {
        try
        {
            Options = ConfigurationManager.Instance.Get<TransportOptions>();
            Options.Validate();
            Logging?.Info($"[SDK.{GetType().Name}] TransportOptions loaded and validated");
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{GetType().Name}] Failed to load TransportOptions: {ex.Message}", ex);
            throw new System.InvalidOperationException("Failed to load TransportOptions", ex);
        }

        if (Catalog is null)
        {
            Logging?.Error($"[SDK.{GetType().Name}] Missing IPacketRegistry");
            throw new System.InvalidOperationException("Missing IPacketRegistry");
        }
    }

    /// <summary>
    /// Creates internal frame sender and receiver helpers.
    /// </summary>
    protected override void CreateFrameHelpers()
    {
        _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, REPORT_BYTES_SENT, HANDLE_SEND_ERROR);
        _receiver = new FRAME_READER(GET_CONNECTED_SOCKET_OR_THROW, Options, HANDLE_RECEIVE_MESSAGE, HANDLE_RECEIVE_ERROR, REPORT_BYTES_RECEIVED);
        Logging?.Debug($"[SDK.{GetType().Name}] Frame helpers created");
    }

    /// <summary>
    /// Starts the background worker responsible for receiving data.
    /// </summary>
    /// <param name="loopToken">Cancellation token controlling the receive loop.</param>
    protected override void StartReceiveWorker(System.Threading.CancellationToken loopToken)
    {
        if (_receiver is null)
        {
            return;
        }

        try
        {
            _receiveHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"TcpSession-Receive-{_host}:{_port}",
                group: "client",
                work: async (_, workerCt) =>
                {
                    var effective = workerCt.CanBeCanceled ? workerCt : loopToken;
                    Logging?.Info($"[SDK.{GetType().Name}] Receive worker started");
                    await _receiver.ReceiveLoopAsync(effective).ConfigureAwait(false);
                },
                options: new WorkerOptions { CancellationToken = loopToken }
            );
        }
        catch (System.Exception ex)
        {
            Logging?.Warn($"[SDK.{GetType().Name}] Failed to schedule receive worker: {ex.Message}, falling back to Task.Run", ex);
            _ = System.Threading.Tasks.Task.Run(() => _receiver.ReceiveLoopAsync(loopToken), loopToken);
        }
    }

    /// <summary>
    /// Connects to the specified TCP endpoint asynchronously.
    /// </summary>
    /// <param name="host">Target host name or IP address.</param>
    /// <param name="port">Target port number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentException">Thrown when host is invalid.</exception>
    /// <exception cref="System.Net.Sockets.SocketException">Thrown when connection fails.</exception>
    public override async System.Threading.Tasks.Task ConnectAsync(System.String? host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpSession));

        System.String? effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
        System.UInt16 effectivePort = port ?? Options.Port;

        if (System.String.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new System.ArgumentException("Host required");
        }

        if (IsConnected &&
            System.String.Equals(_host, effectiveHost, System.StringComparison.OrdinalIgnoreCase) &&
            _port == effectivePort)
        {
            Logging?.Debug($"[SDK.{GetType().Name}] Already connected to {effectiveHost}:{effectivePort}");
            return;
        }

        if (IsConnected)
        {
            Logging?.Debug($"[SDK.{GetType().Name}] Cleaning up existing connection");
            CLEANUP_CONNECTION();
        }

        lock (_sync)
        {
            if (_loopCts is not null)
            {
                CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            }
        }

        using var connectCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (Options.ConnectTimeoutMillis > 0)
        {
            connectCts.CancelAfter(Options.ConnectTimeoutMillis);
        }

        System.Exception? lastEx = null;

        var addrs = await System.Net.Dns.GetHostAddressesAsync(effectiveHost, connectCts.Token);

        foreach (var addr in addrs)
        {
            var s = new System.Net.Sockets.Socket(addr.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            try
            {
                s.NoDelay = Options.NoDelay;
                s.SendBufferSize = Options.BufferSize;
                s.ReceiveBufferSize = Options.BufferSize;

                await s.ConnectAsync(new System.Net.IPEndPoint(addr, effectivePort), connectCts.Token);

                System.Threading.CancellationToken loopToken;

                lock (_sync)
                {
                    _socket = s;
                    _loopCts = new System.Threading.CancellationTokenSource();
                    loopToken = _loopCts.Token;
                    _host = effectiveHost;
                    _port = effectivePort;
                }

                CreateFrameHelpers();

                System.Boolean isReconnect = System.Threading.Interlocked.Exchange(ref _hasEverConnected, 1) == 1;
                if (isReconnect)
                {
                    Logging?.Info($"[SDK.{GetType().Name}] Reconnected to {effectiveHost}:{effectivePort}");
                    OnReconnected?.Invoke(this, 0);
                }
                else
                {
                    Logging?.Info($"[SDK.{GetType().Name}] Connected to {effectiveHost}:{effectivePort}");
                    RaiseConnected();
                }

                StartReceiveWorker(loopToken);
                System.Threading.Interlocked.Exchange(ref _reconnecting, 0);

                return;
            }
            catch (System.Exception ex)
            {
                lastEx = ex;
                Logging?.Warn($"[SDK.{GetType().Name}] Failed to connect to {addr}:{effectivePort}: {ex.Message}", ex);
                try { s.Dispose(); } catch { }
            }
        }

        Logging?.Error($"[SDK.{GetType().Name}] Could not connect to {effectiveHost}:{effectivePort}; last error: {lastEx?.Message}", lastEx);
        throw lastEx ?? new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.HostNotFound);
    }

    /// <inheritdoc/>
    protected override void REPORT_BYTES_SENT(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesSent, count);
        base.REPORT_BYTES_SENT(count);
    }

    /// <inheritdoc/>
    protected override void REPORT_BYTES_RECEIVED(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesReceived, count);
        base.REPORT_BYTES_RECEIVED(count);
    }

    /// <inheritdoc/>
    protected override void HANDLE_SEND_ERROR(System.Exception ex)
    {
        Logging?.Warn($"[SDK.{GetType().Name}] Send error: {ex.Message}", ex);
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    /// <inheritdoc/>
    protected override void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        Logging?.Warn($"[SDK.{GetType().Name}] Receive error: {ex.Message}", ex);
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    private void TriggerReconnect(System.Exception ex)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0)
        {
            Logging?.Info($"[SDK.{GetType().Name}] Triggering auto-reconnect after error: {ex.Message}");
            _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
        }
        else
        {
            Logging?.Trace($"[SDK.{GetType().Name}] Reconnect already in progress, skipping.");
        }
    }

    /// <inheritdoc/>
    protected override void CLEANUP_CONNECTION()
    {
        System.Boolean wasConnected = IsConnected;
        base.CLEANUP_CONNECTION();

        try
        {
            if (_receiveHandle != null)
            {
                try
                {
                    InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                        .CancelWorker(_receiveHandle.Id);
                    _receiveHandle = null;
                    Logging?.Debug($"[SDK.{GetType().Name}] Receive worker cancelled");
                }
                catch
                {
                    Logging?.Warn($"[SDK.{GetType().Name}] Failed to cancel receive worker for {_host}:{_port}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Logging?.Warn($"[SDK.{GetType().Name}] Exception during CLEANUP_CONNECTION: {ex.Message}", ex);
        }
        if (wasConnected)
        {
            Logging?.Info($"[SDK.{GetType().Name}] Disconnected");
            RaiseDisconnected(new System.Exception("Disconnected"));
        }
    }

    internal async System.Threading.Tasks.Task HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(System.Exception cause)
    {
        Logging?.Debug($"[SDK.{GetType().Name}] HANDLE_DISCONNECT_AND_RECONNECT_ASYNC called after: {cause.Message}");
        CLEANUP_CONNECTION();

        if (!Options.ReconnectEnabled || System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        if (System.String.IsNullOrEmpty(_host) || _port == 0)
        {
            return;
        }

        System.Int32 attempt = 0;
        System.Int64 max = System.Math.Max(1, Options.ReconnectMaxDelayMillis);
        System.Int64 delay = System.Math.Max(1, Options.ReconnectBaseDelayMillis);

        while (System.Threading.Volatile.Read(ref _disposed) == 0 &&
               (Options.ReconnectMaxAttempts == 0 || attempt < Options.ReconnectMaxAttempts))
        {
            attempt++;
            System.Int64 jitter = (System.Int64)(Csprng.NextDouble() * delay * 0.3);
            await System.Threading.Tasks.Task.Delay((System.Int32)System.Math.Min(delay + jitter, System.Int32.MaxValue));
            try
            {
                await ConnectAsync(_host, _port);
                Logging?.Info($"[SDK.{GetType().Name}] Successfully reconnected to {_host}:{_port} after {attempt} attempt(s)");
                OnReconnected?.Invoke(this, attempt);
                return;
            }
            catch (System.Exception ex)
            {
                Logging?.Warn($"[SDK.{GetType().Name}] Reconnect attempt {attempt} failed: {ex.Message}", ex);
                delay = System.Math.Min(max, delay * 2);
            }
        }
        Logging?.Error($"[SDK.{GetType().Name}] Reconnect attempts exhausted or stopped");
    }
}