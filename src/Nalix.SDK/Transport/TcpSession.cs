// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Tasks;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.SDK.Transport;

/// <summary>
/// A reliable TCP client that delegates framing, send, and receive responsibilities to internal helpers.
/// Supports automatic reconnection, keep-alive heartbeats, and bandwidth rate sampling.
/// </summary>
/// <remarks>
/// This class is thread-safe for concurrent calls to SendAsync, ConnectAsync, DisconnectAsync, and Dispose.
/// </remarks>
public sealed class TcpSession : IClientConnection
{
    #region Constants

    /// <summary>
    /// Header size in bytes for the framing protocol (2-byte little-endian total length prefix).
    /// </summary>
    public const System.Byte HeaderSize = 2;

    #endregion Constants

    #region Fields

    // Use a plain object for locking. (System.Threading.Lock does not exist in BCL.)
    private readonly System.Threading.Lock _sync = new();

    // Internal frame helpers
    private FRAME_SENDER? _sender;
    private FRAME_READER? _receiver;

    // Socket + loop control
    private System.Net.Sockets.Socket? _socket;
    private System.Threading.CancellationTokenSource? _loopCts;

    // TaskManager-managed worker/recurring handles
    private IWorkerHandle? _receiveHandle;

    // Last known endpoint — stored for automatic reconnection.
    private System.String? _host;
    private System.UInt16? _port = 0;

    // Dispose guard: 0 = live, 1 = disposed.
    private System.Int32 _disposed = 0;

    // Cumulative byte counters (Interlocked)
    internal System.Int64 _bytesSent = 0;
    internal System.Int64 _bytesReceived = 0;

    // Per-interval counters reset by RATE_SAMPLER_TICK
    internal System.Int64? _lastSampleTick = 0;
    internal System.Int64 _sendCounterForInterval = 0;
    internal System.Int64 _receiveCounterForInterval = 0;

    // Last computed bandwidth samples (bytes/s)
    internal System.Int64 _lastSendBps = 0;
    internal System.Int64 _lastReceiveBps = 0;

    // Cached logger — resolved once to avoid repeated DI lookups on hot paths.
    internal static readonly ILogger? s_log = InstanceManager.Instance.GetExistingInstance<ILogger>();

    #endregion Fields

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

    /// <summary>
    /// Raised after a successful automatic reconnection.
    /// </summary>
    public event System.EventHandler<System.Int32>? OnReconnected;

    /// <summary>
    /// Optional asynchronous message handler.
    /// When set, invoked alongside <see cref="OnMessageReceived"/> for async processing scenarios.
    /// </summary>
    public System.Func<TcpSession, System.ReadOnlyMemory<System.Byte>, System.Threading.Tasks.Task>? OnMessageReceivedAsync;

    #endregion Events

    #region Properties

    /// <summary>
    /// Gets the transport options used by this client.
    /// </summary>
    public readonly TransportOptions Options;

    /// <inheritdoc/>
    ITransportOptions IClientConnection.Options => this.Options;

    /// <summary>
    /// Gets the total number of bytes sent since connection.
    /// </summary>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total number of bytes received since connection.
    /// </summary>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    /// <summary>
    /// Gets the average send bandwidth in bytes per second over the last sample interval.
    /// </summary>
    public System.Int64 SendBytesPerSecond => System.Threading.Interlocked.Read(ref _lastSendBps);

    /// <summary>
    /// Gets the average receive bandwidth in bytes per second over the last sample interval.
    /// </summary>
    public System.Int64 ReceiveBytesPerSecond => System.Threading.Interlocked.Read(ref _lastReceiveBps);

    /// <inheritdoc/>
    public System.Boolean IsConnected => _socket?.Connected == true && System.Threading.Volatile.Read(ref _disposed) == 0;

    #endregion Properties

    /// <summary>
    /// Catalog for packet type registration and lookup, used by internal frame helpers for serialization and deserialization.
    /// </summary>
    public static readonly IPacketRegistry Catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
        ?? throw new System.InvalidOperationException("IPacketRegistry instance not found in InstanceManager.");

    #region Static Constructor

    static TcpSession()
    {
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

        InstanceManager.Instance.Register(new BufferPoolManager(bufferConfig));
    }

    #endregion Static Constructor

    #region Constructor

    /// <summary>
    /// Constructs a new <see cref="TcpSession"/> and loads <see cref="TransportOptions"/>.
    /// Falls back to safe defaults if configuration is unavailable.
    /// </summary>
    public TcpSession()
    {
        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Options.Validate();

        // IPacketCatalog is required for deserialization; fail fast with a clear message.
        if (InstanceManager.Instance.GetExistingInstance<IPacketRegistry>() is null)
        {
            s_log?.Error($"[SDK.{nameof(TcpSession)}] No IPacketRegistry instance found; this is a fatal configuration error. The process will terminate.");
            System.Environment.FailFast($"[SDK.{nameof(TcpSession)}] Missing required service: IPacketRegistry.");
        }
    }

    #endregion Constructor

    #region Public API

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task ConnectAsync(System.String? host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpSession));

        // Resolve effective endpoint, falling back to Options.
        System.String? effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
        System.UInt16 effectivePort = port ?? Options.Port;

        if (System.String.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new System.ArgumentException("A host must be provided either as a parameter or via TransportOptions.Address.", nameof(host));
        }

        // If already connected to the same endpoint — no-op.
        if (IsConnected && System.String.Equals(_host, effectiveHost, System.StringComparison.OrdinalIgnoreCase) && _port == effectivePort)
        {
            s_log?.Debug($"[SDK.{nameof(TcpSession)}] ConnectAsync: already connected to {effectiveHost}:{effectivePort} — no-op.");
            return;
        }

        // If connected to a different endpoint, clean up first.
        if (IsConnected)
        {
            s_log?.Info($"[SDK.{nameof(TcpSession)}] ConnectAsync: connected to a different endpoint — cleaning up before reconnect.");
            CLEANUP_CONNECTION();
        }

        // Atomically cancel any previous receive/heartbeat loops.
        lock (_sync)
        {
            if (_loopCts is not null)
            {
                CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            }
        }

        using System.Threading.CancellationTokenSource connectCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (Options.ConnectTimeoutMillis > 0)
        {
            connectCts.CancelAfter(Options.ConnectTimeoutMillis);
        }

        System.Exception? lastEx = null;

        // Use connectCts.Token so DNS resolution is also cancellable/timeout-bound.
        System.Net.IPAddress[] addrs = await System.Net.Dns.GetHostAddressesAsync(effectiveHost, connectCts.Token).ConfigureAwait(false);

        s_log?.Debug($"[SDK.{nameof(TcpSession)}] Resolve {effectiveHost} => {addrs.Length} addresses.");

        foreach (System.Net.IPAddress addr in addrs)
        {
            if (connectCts.IsCancellationRequested)
            {
                s_log?.Warn($"[SDK.{nameof(TcpSession)}] Connect cancelled before attempting {addr}:{effectivePort}.");
                break;
            }

            System.Net.Sockets.Socket s = new(
                addr.AddressFamily,
                System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);

            try
            {
                s.NoDelay = Options.NoDelay;
                s.SendBufferSize = Options.BufferSize;
                s.ReceiveBufferSize = Options.BufferSize;

                s_log?.Trace($"[SDK.{nameof(TcpSession)}] Attempting connect to {addr}:{effectivePort} with timeout {Options.ConnectTimeoutMillis}ms.");

                await s.ConnectAsync(
                    new System.Net.IPEndPoint(addr, effectivePort),
                    connectCts.Token).ConfigureAwait(false);

                // Commit the new socket and a fresh cancellation source under the lock.
                System.Threading.CancellationToken loopToken;
                lock (_sync)
                {
                    _socket = s;
                    _loopCts = new System.Threading.CancellationTokenSource();
                    loopToken = _loopCts.Token;

                    // Store resolved endpoint for auto-reconnect.
                    _host = effectiveHost;
                    _port = effectivePort;
                }

                // Build frame helpers bound to the socket getter.
                _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, REPORT_BYTES_SENT, HANDLE_SEND_ERROR);
                _receiver = new FRAME_READER(GET_CONNECTED_SOCKET_OR_THROW, Options, HANDLE_RECEIVE_MESSAGE, HANDLE_RECEIVE_ERROR, REPORT_BYTES_RECEIVED);

                s_log?.Info($"[SDK.{nameof(TcpSession)}] Connected remote={addr}:{effectivePort}");
                try { OnConnected?.Invoke(this, System.EventArgs.Empty); } catch { /* swallow subscriber errors */ }

                START_RECEIVE_WORKER(addr, effectivePort, loopToken);

                return; // Connected successfully.
            }
            catch (System.Exception ex) when (ex is not System.OperationCanceledException oce || !connectCts.IsCancellationRequested)
            {
                lastEx = ex;
                s_log?.Warn($"[SDK.{nameof(TcpSession)}] connect-failed addr={addr}:{effectivePort} ex={ex.Message}");

                try
                {
                    s.Dispose();
                }
                catch { /* best-effort */ }
            }
        }

        // All addresses failed.
        throw lastEx
            ?? new System.Net.Sockets.SocketException(
                (System.Int32)System.Net.Sockets.SocketError.HostNotFound);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task DisconnectAsync()
    {
        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        CLEANUP_CONNECTION();

        s_log?.Info($"[SDK.{nameof(TcpSession)}] Disconnected (requested).");
        try { OnDisconnected?.Invoke(this, null!); } catch { }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpSession));
        FRAME_SENDER? sender = System.Threading.Volatile.Read(ref _sender);

        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(payload, ct);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync([System.Diagnostics.CodeAnalysis.NotNull] IPacket packet, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpSession));
        FRAME_SENDER? sender = System.Threading.Volatile.Read(ref _sender);

        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(packet, ct);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Atomic flip: only the first caller proceeds.
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        CLEANUP_CONNECTION();

        s_log?.Info($"[SDK.{nameof(TcpSession)}] Disposed.");
        System.GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Private — Connection Lifecycle

    private void START_RECEIVE_WORKER(
        System.Net.IPAddress addr,
        System.UInt16 port,
        System.Threading.CancellationToken loopToken)
    {
        if (_receiver is null)
        {
            s_log?.Warn($"[SDK.{nameof(TcpSession)}] Attempted to start receive worker with null receiver.");
            return;
        }

        try
        {
            _receiveHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"TcpSession-Receive-{addr}:{port}",
                group: "client",
                work: async (_, workerCt) =>
                {
                    System.Threading.CancellationToken effective =
                        workerCt.CanBeCanceled ? workerCt : loopToken;
                    await _receiver!.ReceiveLoopAsync(effective).ConfigureAwait(false);
                },
                options: new WorkerOptions { CancellationToken = loopToken, Tag = TaskNaming.Tags.Service }
            );
        }
        catch (System.Exception ex)
        {
            s_log?.Warn(
                $"[SDK.{nameof(TcpSession)}] schedule-receive-failed; falling back to Task.Run. ex={ex.Message}");

            _ = System.Threading.Tasks.Task.Run(
                () => _receiver!.ReceiveLoopAsync(loopToken),
                System.Threading.CancellationToken.None);
        }
    }

    private void CLEANUP_CONNECTION()
    {
        lock (_sync)
        {
            if (_loopCts is not null)
            {
                CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            }

            // Dispose sender if present.
            try
            {
                var prevSender = System.Threading.Interlocked.Exchange(ref _sender, null);
                prevSender?.Dispose();
            }
            catch { /* swallow */ }

            try
            {
                _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both);
            }
            catch { }

            try
            {
                _socket?.Close(); _socket?.Dispose();
            }
            catch { }

            _socket = null!;
        }

        // Cancel TaskManager handles (best-effort; individual failures must not abort cleanup).
        try
        {
            if (_receiveHandle is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_receiveHandle.Id);
                _receiveHandle = null!;
            }
        }
        catch { /* best-effort; swallow */ }
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

    #endregion Private — Connection Lifecycle

    #region Private — Callbacks

    private void REPORT_BYTES_SENT(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesSent, count);
        System.Threading.Interlocked.Add(ref _sendCounterForInterval, count);

        try
        {
            OnBytesSent?.Invoke(this, count);
        }
        catch { /* swallow subscriber exceptions */ }
    }

    private void REPORT_BYTES_RECEIVED(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesReceived, count);
        System.Threading.Interlocked.Add(ref _receiveCounterForInterval, count);

        try
        {
            OnBytesReceived?.Invoke(this, count);
        }
        catch { /* swallow subscriber exceptions */ }
    }

    private void HANDLE_SEND_ERROR(System.Exception ex)
    {
        try
        {
            OnError?.Invoke(this, ex);
        }
        catch { }

        _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
    }

    private void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        try
        {
            OnError?.Invoke(this, ex);
        }
        catch { }

        _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    private void HANDLE_RECEIVE_MESSAGE(BufferLease lease)
    {
        // Snapshot invocation list of synchronous handlers.
        System.Delegate[] syncHandlers = OnMessageReceived?.GetInvocationList() ?? System.Array.Empty<System.Delegate>();

        var asyncHandler = OnMessageReceivedAsync;

        // Prepare async data copy BEFORE disposing lease to avoid using released memory.
        System.ReadOnlyMemory<System.Byte> asyncData = asyncHandler is not null ? lease.Span.ToArray() : System.ReadOnlyMemory<System.Byte>.Empty;

        try
        {
            // Deliver to each synchronous subscriber.
            foreach (System.Delegate d in syncHandlers)
            {
                BufferLease copy = BufferLease.CopyFrom(lease.Span);
                System.Boolean disposedCopy = false;
                try
                {
                    // Cast to EventHandler<IBufferLease> because the event is declared as IBufferLease.
                    ((System.EventHandler<IBufferLease>)d).Invoke(this, copy);
                }
                catch (System.Exception ex)
                {
                    // Ensure the copy gets disposed if subscriber threw before its wrapper disposed it.
                    try { copy.Dispose(); disposedCopy = true; } catch { }
                    s_log?.Error($"[SDK.{nameof(TcpSession)}] sync-handler-faulted: {ex.Message}", ex);
                }
                finally
                {
                    // If subscriber wrapper didn't dispose the copy (because it isn't following contract),
                    // best-effort dispose here to avoid leaks.
                    if (!disposedCopy)
                    {
                        try { /* assume subscriber disposed; if not, it's already disposed by wrapper. */ } catch { }
                    }
                }
            }
        }
        finally
        {
            // Dispose original lease once for all sync handlers.
            try { lease.Dispose(); } catch { }
        }

        // Fire async handler (if present). Run fire-and-forget but log failures.
        if (asyncHandler is not null)
        {
            _ = asyncHandler(this, asyncData).ContinueWith(
                t => s_log?.Error($"[SDK.{nameof(TcpSession)}] OnMessageReceivedAsync faulted: {t.Exception?.GetBaseException().Message}"),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    #endregion Private �� Callbacks

    #region Private — Background Loops

    internal async System.Threading.Tasks.Task HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(System.Exception cause)
    {
        try { OnDisconnected?.Invoke(this, cause); } catch { }

        // Tear down current socket AND cancel all background tasks before reconnect.
        CLEANUP_CONNECTION();

        if (!Options.ReconnectEnabled || System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        if (System.String.IsNullOrEmpty(_host) || _port == 0)
        {
            s_log?.Info($"[SDK.{nameof(TcpSession)}] No saved endpoint; skipping auto-reconnect.");
            return;
        }

        System.Int32 attempt = 0;
        System.Int64 delayMs = System.Math.Max(1L, Options.ReconnectBaseDelayMillis);
        System.Int64 maxDelayMs = System.Math.Max(1L, Options.ReconnectMaxDelayMillis);

        while (System.Threading.Volatile.Read(ref _disposed) == 0 &&
               (Options.ReconnectMaxAttempts == 0 || attempt < Options.ReconnectMaxAttempts))
        {
            attempt++;
            s_log?.Info($"[SDK.{nameof(TcpSession)}] Reconnect attempt={attempt} delay={delayMs} ms.");

            System.Int64 jitter = (System.Int64)(Csprng.NextDouble() * delayMs * 0.3);
            await System.Threading.Tasks.Task.Delay((System.Int32)System.Math.Min(delayMs + jitter, System.Int32.MaxValue)).ConfigureAwait(false);

            try
            {
                await ConnectAsync(_host, _port).ConfigureAwait(false);
                s_log?.Info($"[SDK.{nameof(TcpSession)}] Reconnect-success attempt={attempt}.");

                try { OnReconnected?.Invoke(this, attempt); } catch { }

                return;
            }
            catch (System.Exception ex)
            {
                s_log?.Warn($"[SDK.{nameof(TcpSession)}] Reconnect-failed attempt={attempt} ex={ex.Message}");
                try { OnError?.Invoke(this, ex); } catch { }

                delayMs = System.Math.Min(maxDelayMs, delayMs * 2);
            }
        }

        s_log?.Info($"[SDK.{nameof(TcpSession)}] Reconnect attempts exhausted.");
    }

    #endregion Private — Background Loops

    #region Private — Helpers

    private System.Net.Sockets.Socket GET_CONNECTED_SOCKET_OR_THROW()
    {
        System.Net.Sockets.Socket? s = _socket;

        return s?.Connected == true
            ? s
            : throw new System.InvalidOperationException("Client not connected.");
    }

    #endregion Private — Helpers
}