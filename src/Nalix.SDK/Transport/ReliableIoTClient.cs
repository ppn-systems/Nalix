// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport;

/// <summary>
/// This class is thread-safe. Many APIs/fields copied from <see cref="ReliableClient"/>.
/// </summary>
public sealed class ReliableIoTClient : IClientConnection
{
    #region Fields

    private readonly System.Threading.Lock _sync = new();

    // Internal helpers
    private FRAME_SENDER _sender;
    private FRAME_READER _receiver;

    // Socket + loop control
    private System.Net.Sockets.Socket _socket;
    private System.Threading.CancellationTokenSource _loopCts;

    // Last known endpoint for auto-reconnect etc.
    private System.Threading.Tasks.Task _receiveTask;

    // Dispose guard.
    private System.Int32 _disposed;

    // Cached logger
    private readonly ILogger _log;

    #endregion

    #region Events

    /// <inheritdoc/>
    public event System.EventHandler OnConnected;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception> OnError;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64> OnBytesSent;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64> OnBytesReceived;

    /// <inheritdoc/>
    public event System.EventHandler<IBufferLease> OnMessageReceived;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception> OnDisconnected;

    #endregion

    #region Properties

    /// <inheritdoc/>
    public readonly TransportOptions Options;

    ITransportOptions IClientConnection.Options => this.Options;

    /// <inheritdoc/>
    public System.Boolean IsConnected => _socket?.Connected == true && System.Threading.Volatile.Read(ref _disposed) == 0;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructs a new <see cref="ReliableIoTClient"/> instance; loads <see cref="TransportOptions"/>.
    /// </summary>
    public ReliableIoTClient()
    {
        _log = InstanceManager.Instance.GetExistingInstance<ILogger>();

        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Options.Validate();

        BufferConfig bufferConfig = ConfigurationManager.Instance.Get<BufferConfig>();

        bufferConfig.TotalBuffers = 8;                      // Cực kỳ ít buffer, chỉ đủ dùng tối thiểu
        bufferConfig.EnableMemoryTrimming = true;           // Luôn cần giải phóng
        bufferConfig.TrimIntervalMinutes = 1;
        bufferConfig.DeepTrimIntervalMinutes = 3;
        bufferConfig.EnableAnalytics = false;
        bufferConfig.AdaptiveGrowthFactor = 1.0;            // Không tăng số lượng tự động
        bufferConfig.MaxMemoryPercentage = 0.01;            // Chỉ cho phép dùng 1% RAM (rất giới hạn!)
        bufferConfig.SecureClear = false;                   // Không cần clear trừ khi dữ liệu nhạy c��m
        bufferConfig.EnableQueueCompaction = false;
        bufferConfig.AutoTuneOperationThreshold = 8;
        bufferConfig.FallbackToArrayPool = false;           // Tránh fallback do không muốn ngốn RAM bất ngờ
        bufferConfig.ExpandThresholdPercent = 0.10;
        bufferConfig.ShrinkThresholdPercent = 0.30;
        bufferConfig.MinimumIncrease = 1;
        bufferConfig.MaxBufferIncreaseLimit = 2;            // Mỗi lần tăng chỉ 1-2 buffer
        bufferConfig.BufferAllocations = "128,0.50; 256,0.50"; // Chỉ giữ các size nhỏ
        bufferConfig.MaxMemoryBytes = 32 * 1024;            // Tối đa 32 KB cho buffer (cá nhân hóa tùy RAM thiết bị IoT)

        bufferConfig.Validate();
    }

    #endregion

    #region Public API

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task ConnectAsync(System.String host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(ReliableIoTClient));

        System.String effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
        System.UInt16 effectivePort = port ?? Options.Port;

        // Basic validation for host
        if (System.String.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new System.ArgumentException("A valid host must be provided.", nameof(host));
        }

        // Guard: already connected
        if (IsConnected)
        {
            return;
        }

        // Cancel previous tasks gracefully
        lock (_sync)
        {
            CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
        }

        // Retry logic using exponential backoff (retry maximum of 3 times for IoT)
        const System.Int32 maxRetries = 3;
        System.Exception lastException = null;

        for (System.Int32 retryCount = 0; retryCount < maxRetries; retryCount++)
        {
            try
            {
                // Resolve all addresses for the host
                var addresses = await System.Net.Dns.GetHostAddressesAsync(effectiveHost, ct).ConfigureAwait(false);

                foreach (var address in addresses)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break; // Exit loop on cancellation
                    }

                    using var socket = new System.Net.Sockets.Socket(address.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                    socket.NoDelay = Options.NoDelay;
                    socket.SendBufferSize = Options.BufferSize;
                    socket.ReceiveBufferSize = Options.BufferSize;

                    // Attempt to connect
                    await socket.ConnectAsync(new System.Net.IPEndPoint(address, effectivePort), ct).ConfigureAwait(false);

                    // Update internal fields & initialize sender/receiver
                    lock (_sync)
                    {
                        _socket = socket;
                        _loopCts = new System.Threading.CancellationTokenSource();
                        _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, REPORT_BYTES_SENT, HANDLE_SEND_ERROR);
                        _receiver = new FRAME_READER(GET_CONNECTED_SOCKET_OR_THROW, Options, HANDLE_RECEIVE_MESSAGE, HANDLE_RECEIVE_ERROR, REPORT_BYTES_RECEIVED);
                    }

                    // Notify connection successful
                    _log?.Info($"[SDK.{nameof(ReliableIoTClient)}] Connected to {address}:{effectivePort}");
                    OnConnected?.Invoke(this, System.EventArgs.Empty);

                    START_RECEIVE_WORKER_IOT(_loopCts.Token);
                    return; // Successful connection
                }
            }
            catch (System.Exception ex) when (ex is not System.OperationCanceledException)
            {
                lastException = ex;
                _log?.Warn($"[SDK.{nameof(ReliableIoTClient)}] Connection attempt {retryCount + 1}/{maxRetries} failed: {ex.Message}");

                // Wait for increasing "backoff" time before retrying
                await System.Threading.Tasks.Task.Delay((retryCount + 1) * 1000, ct).ConfigureAwait(false);
            }
        }

        throw lastException ?? new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.HostNotFound);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task DisconnectAsync()
    {
        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        CLEANUP_CONNECTION();
        _log?.Info($"[SDK.{nameof(ReliableIoTClient)}] Disconnected (requested).");
        OnDisconnected?.Invoke(this, null);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(ReliableIoTClient));
        var sender = System.Threading.Volatile.Read(ref _sender);
        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(payload, ct);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(IPacket packet, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(ReliableIoTClient));
        var sender = System.Threading.Volatile.Read(ref _sender);
        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(packet, ct);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        CLEANUP_CONNECTION();
        _receiveTask.Dispose();
        _log?.Info($"[SDK.{nameof(ReliableIoTClient)}] Disposed.");
        System.GC.SuppressFinalize(this);
    }

    #endregion

    #region Private - Worker/Loops

    private void START_RECEIVE_WORKER_IOT(System.Threading.CancellationToken loopToken)
    {
        _receiveTask = System.Threading.Tasks.Task.Run(async () =>
        {
            try { await _receiver.ReceiveLoopAsync(loopToken); }
            catch (System.Exception ex) { HANDLE_RECEIVE_ERROR(ex); }
        }, System.Threading.CancellationToken.None);
    }
    #endregion

    #region Private - Cleanup

    private void CLEANUP_CONNECTION()
    {
        lock (_sync)
        {
            CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);

            System.Threading.Interlocked.Exchange(ref _sender, null)?.Dispose();

            try { _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _socket?.Close(); _socket?.Dispose(); } catch { }
            _socket = null;
            _receiver = null;
        }

        _receiveTask = null;
    }

    private static void CANCEL_AND_DISPOSE_LOCKED(ref System.Threading.CancellationTokenSource cts)
    {
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }
        cts = null;
    }

    #endregion

    #region Private - Callbacks & Helpers

    private void REPORT_BYTES_SENT(System.Int32 count)
    {
        try { OnBytesSent?.Invoke(this, count); } catch { }
    }

    private void REPORT_BYTES_RECEIVED(System.Int32 count)
    {
        try { OnBytesReceived?.Invoke(this, count); } catch { }
    }

    private void HANDLE_SEND_ERROR(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
    }

    private void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
    }

    private void HANDLE_RECEIVE_MESSAGE(BufferLease lease)
    {
        // This is similar to ReliableClient; see sample for invoke logic.
        var syncHandlers = OnMessageReceived?.GetInvocationList();
        try
        {
            if (syncHandlers != null)
            {
                foreach (var d in syncHandlers)
                {
                    var copy = BufferLease.CopyFrom(lease.Span);
                    try
                    {
                        ((System.EventHandler<BufferLease>)d).Invoke(this, copy);
                    }
                    catch
                    {
                        try { copy.Dispose(); } catch { }
                    }
                }
            }
        }
        finally
        {
            try { lease.Dispose(); } catch { }
        }
    }

    private System.Net.Sockets.Socket GET_CONNECTED_SOCKET_OR_THROW()
    {
        var s = _socket;
        return s?.Connected == true
            ? s
            : throw new System.InvalidOperationException("Client not connected.");
    }

    #endregion Private - Callbacks & Helpers
}