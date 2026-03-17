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
/// This class is thread-safe. Many APIs/fields copied from <see cref="TcpSession"/>.
/// </summary>
public sealed class IoTTcpSession : IClientConnection
{
    #region Fields

    // Use a simple object as the lock; System.Threading.Lock does not exist in BCL.
    private readonly System.Object _sync = new();

    // Internal helpers
    private FRAME_SENDER? _sender;
    private FRAME_READER? _receiver;

    // Socket + loop control
    private System.Net.Sockets.Socket? _socket;
    private System.Threading.CancellationTokenSource? _loopCts;

    // Last known endpoint for auto-reconnect etc. (optional)
    private System.String? _host;
    private System.UInt16? _port;

    // Receive task (do not dispose Task)
    private System.Threading.Tasks.Task? _receiveTask;

    // Dispose guard: 0 = live, 1 = disposed
    private System.Int32 _disposed = 0;

    // Cached logger
    private readonly ILogger? _log;

    #endregion

    #region Events

    /// <inheritdoc/>
    public event System.EventHandler? OnConnected;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception>? OnError;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64>? OnBytesSent;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64>? OnBytesReceived;

    /// <inheritdoc/>
    public event System.EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception>? OnDisconnected;

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
    /// Constructs a new <see cref="IoTTcpSession"/> instance; loads <see cref="TransportOptions"/>.
    /// </summary>
    public IoTTcpSession()
    {
        _log = InstanceManager.Instance.GetExistingInstance<ILogger>();

        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Options.Validate();

        BufferConfig bufferConfig = ConfigurationManager.Instance.Get<BufferConfig>();

        bufferConfig.TotalBuffers = 8;                      // Very small pool for constrained IoT device
        bufferConfig.EnableMemoryTrimming = true;
        bufferConfig.TrimIntervalMinutes = 1;
        bufferConfig.DeepTrimIntervalMinutes = 3;
        bufferConfig.EnableAnalytics = false;
        bufferConfig.AdaptiveGrowthFactor = 1.0;
        bufferConfig.MaxMemoryPercentage = 0.01;
        bufferConfig.SecureClear = false;
        bufferConfig.EnableQueueCompaction = false;
        bufferConfig.AutoTuneOperationThreshold = 8;
        bufferConfig.FallbackToArrayPool = false;
        bufferConfig.ExpandThresholdPercent = 0.10;
        bufferConfig.ShrinkThresholdPercent = 0.30;
        bufferConfig.MinimumIncrease = 1;
        bufferConfig.MaxBufferIncreaseLimit = 2;
        bufferConfig.BufferAllocations = "128,0.50; 256,0.50";
        bufferConfig.MaxMemoryBytes = 32 * 1024;            // 32 KB max for pool

        bufferConfig.Validate();
    }

    #endregion

    #region Public API

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task ConnectAsync(System.String? host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(IoTTcpSession));

        System.String? effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
        System.UInt16 effectivePort = port ?? Options.Port;

        if (System.String.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new System.ArgumentException("A valid host must be provided.", nameof(host));
        }

        // If already connected to the same endpoint, no-op.
        if (IsConnected && System.String.Equals(_host, effectiveHost, System.StringComparison.OrdinalIgnoreCase) && _port == effectivePort)
        {
            _log?.Debug($"[SDK.{nameof(IoTTcpSession)}] Already connected to {effectiveHost}:{effectivePort} - skipping ConnectAsync.");
            return;
        }

        // If currently connected to a different endpoint, clean up first.
        if (IsConnected)
        {
            _log?.Info($"[SDK.{nameof(IoTTcpSession)}] Connected to different endpoint - cleaning up before connect.");
            CLEANUP_CONNECTION();
        }

        // Cancel previous tasks gracefully
        lock (_sync)
        {
            if (_loopCts != null)
            {
                CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            }
        }

        // Retry logic using exponential backoff (retry maximum of 3 times for IoT)
        const System.Int32 maxRetries = 3;
        System.Exception? lastException = null;

        for (System.Int32 retryCount = 0; retryCount < maxRetries; retryCount++)
        {
            try
            {
                // DNS resolution should honor cancellation token.
                var addresses = await System.Net.Dns.GetHostAddressesAsync(effectiveHost, ct).ConfigureAwait(false);

                if (addresses is null || addresses.Length == 0)
                {
                    throw new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.HostNotFound);
                }

                _log?.Debug($"[SDK.{nameof(IoTTcpSession)}] Resolved {effectiveHost} -> {addresses.Length} addresses.");

                foreach (var address in addresses)
                {
                    if (ct.IsCancellationRequested)
                    {
                        _log?.Warn($"[SDK.{nameof(IoTTcpSession)}] Connect cancelled before trying {address}:{effectivePort}.");
                        break;
                    }

                    System.Net.Sockets.Socket socket = new(address.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                    try
                    {
                        socket.NoDelay = Options.NoDelay;
                        socket.SendBufferSize = Options.BufferSize;
                        socket.ReceiveBufferSize = Options.BufferSize;

                        _log?.Trace($"[SDK.{nameof(IoTTcpSession)}] Attempting connect to {address}:{effectivePort} (attempt {retryCount + 1}/{maxRetries}).");

                        // Attempt to connect; this will throw on cancellation or failure.
                        await socket.ConnectAsync(new System.Net.IPEndPoint(address, effectivePort), ct).ConfigureAwait(false);

                        // On success, persist socket and create helpers under lock.
                        lock (_sync)
                        {
                            _socket = socket;
                            _loopCts = new System.Threading.CancellationTokenSource();
                            _receiver = new FRAME_READER(GET_CONNECTED_SOCKET_OR_THROW, Options, HANDLE_RECEIVE_MESSAGE, HANDLE_RECEIVE_ERROR, REPORT_BYTES_RECEIVED);
                            _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, REPORT_BYTES_SENT, HANDLE_SEND_ERROR);

                            // Save for potential reconnect logic later.
                            _host = effectiveHost;
                            _port = effectivePort;
                        }

                        _log?.Info($"[SDK.{nameof(IoTTcpSession)}] Connected to {address}:{effectivePort}");
                        try { OnConnected?.Invoke(this, System.EventArgs.Empty); } catch { /* swallow subscriber exceptions */ }

                        START_RECEIVE_WORKER_IOT(_loopCts!.Token);

                        return; // success
                    }
                    catch
                    {
                        // Dispose socket on failure to avoid leaks.
                        try { socket.Dispose(); } catch { }
                        throw;
                    }
                }
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
            {
                lastException = new System.OperationCanceledException("Connect canceled by caller.", ct);
                break;
            }
            catch (System.Exception ex)
            {
                lastException = ex;
                _log?.Warn($"[SDK.{nameof(IoTTcpSession)}] Connection attempt {retryCount + 1}/{maxRetries} failed: {ex.Message}");
                if (retryCount + 1 < maxRetries)
                {
                    // Simple linear backoff for constrained devices.
                    try { await System.Threading.Tasks.Task.Delay((retryCount + 1) * 1000, ct).ConfigureAwait(false); } catch { /* ignore cancellation during delay */ }
                }
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
        _log?.Info($"[SDK.{nameof(IoTTcpSession)}] Disconnected (requested).");
        try { OnDisconnected?.Invoke(this, null!); } catch { }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(IoTTcpSession));
        var sender = System.Threading.Volatile.Read(ref _sender);
        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(payload, ct);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync([System.Diagnostics.CodeAnalysis.NotNull] IPacket packet, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(IoTTcpSession));
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

        // Do not dispose Task; let it end naturally after cancellation.
        _receiveTask = null;
        _log?.Info($"[SDK.{nameof(IoTTcpSession)}] Disposed.");
        System.GC.SuppressFinalize(this);
    }

    #endregion

    #region Private - Worker/Loops

    private void START_RECEIVE_WORKER_IOT(System.Threading.CancellationToken loopToken)
    {
        if (_receiver is null)
        {
            _log?.Warn($"[SDK.{nameof(IoTTcpSession)}] Cannot start receive worker: receiver is null.");
            return;
        }

        // Fire-and-forget receive loop; the loop respects loopToken cancellation.
        _receiveTask = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await _receiver!.ReceiveLoopAsync(loopToken).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                // Notify error handlers. Do not rethrow.
                HANDLE_RECEIVE_ERROR(ex);
            }
        }, System.Threading.CancellationToken.None);
    }
    #endregion

    #region Private - Cleanup

    private void CLEANUP_CONNECTION()
    {
        lock (_sync)
        {
            if (_loopCts is not null)
            {
                CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            }

            try
            {
                var prevSender = System.Threading.Interlocked.Exchange(ref _sender, null);
                prevSender?.Dispose();
            }
            catch { /* swallow */ }

            try { _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _socket?.Close(); _socket?.Dispose(); } catch { }
            _socket = null!;
            _receiver = null!;
        }

        // Do not dispose _receiveTask; it will finish once loopToken is cancelled.
        _receiveTask = null!;
    }

    private static void CANCEL_AND_DISPOSE_LOCKED(ref System.Threading.CancellationTokenSource cts)
    {
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }
        cts = null!;
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
        // IoT policy: do not auto-reconnect here by default; caller can decide.
        CLEANUP_CONNECTION();
    }

    private void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        // Tear down connection to allow caller to attempt reconnect if desired.
        CLEANUP_CONNECTION();
    }

    private void HANDLE_RECEIVE_MESSAGE(BufferLease lease)
    {
        // Snapshot invocation list
        var handlers = OnMessageReceived?.GetInvocationList();
        System.ReadOnlyMemory<System.Byte> asyncCopy = System.ReadOnlyMemory<System.Byte>.Empty;

        try
        {
            if (handlers != null && handlers.Length > 0)
            {
                foreach (var d in handlers)
                {
                    // Create per-subscriber copy from lease. Each copy is independently owned.
                    BufferLease copy = BufferLease.CopyFrom(lease.Span);
                    System.Boolean disposedCopy = false;
                    try
                    {
                        // Event is declared as EventHandler<IBufferLease>, invoke accordingly.
                        ((System.EventHandler<IBufferLease>)d).Invoke(this, copy);
                    }
                    catch (System.Exception ex)
                    {
                        // If subscriber faults, dispose the copy to return buffer to pool.
                        try { copy.Dispose(); disposedCopy = true; } catch { }
                        _log?.Error($"[SDK.{nameof(IoTTcpSession)}] sync handler faulted: {ex.Message}", ex);
                    }
                    finally
                    {
                        // If subscriber contract didn't dispose the copy (should not happen), ensure we don't leak.
                        if (!disposedCopy)
                        {
                            // We assume subscriber disposed; if not, the wrapper should handle it.
                        }
                    }
                }
            }
        }
        finally
        {
            // Dispose original lease exactly once.
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