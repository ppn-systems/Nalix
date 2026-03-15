// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Extensions;
using Nalix.SDK.Transport.Internal;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport;

/// <summary>
/// A reliable TCP client that delegates framing, send, and receive responsibilities to internal helpers.
/// Supports automatic reconnection, keep-alive heartbeats, and bandwidth rate sampling.
/// </summary>
/// <remarks>
/// This class is <b>thread-safe</b> for concurrent calls to <see cref="SendAsync(IPacket, System.Threading.CancellationToken)"/>,
/// <see cref="ConnectAsync"/>, <see cref="DisconnectAsync"/>, and <see cref="Dispose"/>.
/// </remarks>
public sealed class ReliableClient : IClientConnection
{
    #region Constants

    /// <summary>
    /// Header size in bytes for the framing protocol (2-byte little-endian total length prefix).
    /// </summary>
    public const System.Byte HeaderSize = 2;

    #endregion Constants

    #region Fields

    private readonly System.Threading.Lock _sync = new();

    // Internal frame helpers
    private FRAME_SENDER _sender;
    private FRAME_READER _receiver;

    // Socket + loop control
    private System.Net.Sockets.Socket _socket;
    private System.Threading.CancellationTokenSource _loopCts;

    // TaskManager-managed worker/recurring handles
    private IWorkerHandle _receiveHandle;
    private System.String _heartbeatName;
    private System.String _rateSamplerName;

    // Last known endpoint — stored for automatic reconnection.
    private System.String _host;
    private System.UInt16 _port;

    // Dispose guard: 0 = live, 1 = disposed.
    // Using int instead of volatile bool enables Interlocked.CompareExchange for atomic flip.
    private System.Int32 _disposed;

    // Cumulative byte counters (Interlocked)
    private System.Int64 _bytesSent;
    private System.Int64 _bytesReceived;

    // Per-interval counters reset by RATE_SAMPLER_TICK
    private System.Int64 _lastSampleTick;
    private System.Int64 _sendCounterForInterval;
    private System.Int64 _receiveCounterForInterval;

    // Last computed bandwidth samples (bytes/s)
    private System.Int64 _lastSendBps;
    private System.Int64 _lastReceiveBps;

    // RTT (ms) của lần heartbeat gần nhất
    private Control _lastHeartbeatPong;
    private System.Double _lastHeartbeatRtt;
    private readonly System.Threading.Lock _heartbeatLock = new();

    // Cached logger — resolved once to avoid repeated DI lookups on hot paths.
    private readonly ILogger _log;

    #endregion Fields

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

    /// <summary>
    /// Optional asynchronous message handler.
    /// When set, invoked alongside <see cref="OnMessageReceived"/> for async processing scenarios.
    /// </summary>
    /// <remarks>
    /// Unlike the event, this is a single-delegate slot to avoid multicast complications with async.
    /// The caller is responsible for disposing the <see cref="IBufferLease"/> if they consume it here.
    /// </remarks>
    public System.Func<ReliableClient, System.ReadOnlyMemory<System.Byte>, System.Threading.Tasks.Task> OnMessageReceivedAsync;

    #endregion Events

    #region Properties

    /// <summary>
    /// Gets the transport options used by this client.
    /// </summary>
    public readonly TransportOptions Options;

    /// <inheritdoc/>
    ITransportOptions IClientConnection.Options => this.Options;

    /// <summary>
    /// RTT (ms) of the most recent heartbeat (if not available, value = 0)
    /// </summary>
    public System.Double LastHeartbeatRtt
    {
        get { lock (_heartbeatLock) { return _lastHeartbeatRtt; } }
    }

    /// <summary>
    /// The most recent PONG control packet received (or null if not received)
    /// </summary>
    public Control LastHeartbeatPong
    {
        get { lock (_heartbeatLock) { return _lastHeartbeatPong; } }
    }

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

    #region Constructor

    /// <summary>
    /// Constructs a new <see cref="ReliableClient"/> and loads <see cref="TransportOptions"/> via
    /// <see cref="ConfigurationManager"/>. Falls back to safe defaults if configuration is unavailable.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown (via <see cref="System.Environment.FailFast(System.String)"/>) when <see cref="IPacketRegistry"/>
    /// is not registered — this is an unrecoverable misconfiguration.
    /// </exception>
    public ReliableClient()
    {
        // Cache logger immediately; null-safe via ?. on all call sites.
        _log = InstanceManager.Instance.GetExistingInstance<ILogger>();

        try
        {
            Options = ConfigurationManager.Instance.Get<TransportOptions>();
        }
        catch
        {
            Options = new TransportOptions
            {
                NoDelay = true,
                BufferSize = 8192,
                ReconnectEnabled = true,
                ReconnectMaxAttempts = 0,
                ConnectTimeoutMillis = 5000,
                KeepAliveIntervalMillis = 0,
                ReconnectBaseDelayMillis = 500,
                ReconnectMaxDelayMillis = 30000,
                MaxPacketSize = PacketConstants.PacketSizeLimit
            };
        }

        // IPacketCatalog is required for deserialization; fail fast with a clear message.
        if (InstanceManager.Instance.GetExistingInstance<IPacketRegistry>() is null)
        {
            _log?.Error($"[SDK.{nameof(ReliableClient)}] No IPacketRegistry instance found; this is a fatal configuration error. The process will terminate.");

            System.Environment.FailFast($"[SDK.{nameof(ReliableClient)}] Missing required service: IPacketRegistry.");
        }
    }

    #endregion Constructor

    #region Public API

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves DNS for the provided host and attempts each returned address in order.
    /// The entire operation is bounded by <see cref="TransportOptions.ConnectTimeoutMillis"/>
    /// when that value is greater than zero.
    /// If <paramref name="host"/> is <see langword="null"/> or whitespace,
    /// <see cref="TransportOptions.Address"/> is used as the fallback.
    /// </remarks>
    /// <exception cref="System.ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="System.Net.Sockets.SocketException">Thrown when all resolved addresses fail to connect.</exception>
    public async System.Threading.Tasks.Task ConnectAsync(System.String host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(ReliableClient));

        // Resolve effective endpoint, falling back to Options.
        System.String effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
        System.UInt16 effectivePort = port ?? Options.Port;

        if (System.String.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new System.ArgumentException("A host must be provided either as a parameter or via TransportOptions.Address.", nameof(host));
        }

        // Guard: already connected to the same endpoint — no-op.
        if (IsConnected)
        {
            return;
        }

        // Atomically cancel any previous receive/heartbeat loops.
        lock (_sync)
        {
            CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
        }

        using System.Threading.CancellationTokenSource connectCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (Options.ConnectTimeoutMillis > 0)
        {
            connectCts.CancelAfter(Options.ConnectTimeoutMillis);
        }

        System.Exception lastEx = null;

        System.Net.IPAddress[] addrs = await System.Net.Dns.GetHostAddressesAsync(effectiveHost, ct).ConfigureAwait(false);

        foreach (System.Net.IPAddress addr in addrs)
        {
            if (connectCts.IsCancellationRequested)
            {
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
                _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, ReportBytesSent, HandleSendError);
                _receiver = new FRAME_READER(GET_CONNECTED_SOCKET_OR_THROW, Options, HandleReceiveMessage, HandleReceiveError, ReportBytesReceived);

                _log?.Info($"[SDK.{nameof(ReliableClient)}] Connected remote={addr}:{effectivePort}");
                OnConnected?.Invoke(this, System.EventArgs.Empty);

                START_RATE_SAMPLER(addr, effectivePort, loopToken);
                START_RECEIVE_WORKER(addr, effectivePort, loopToken);
                START_HEARTBEAT(addr, effectivePort, loopToken);

                return; // Connected successfully.
            }
            catch (System.Exception ex) when (ex is not System.OperationCanceledException oce || !connectCts.IsCancellationRequested)
            {
                lastEx = ex;
                _log?.Warn($"[SDK.{nameof(ReliableClient)}] connect-failed addr={addr}:{effectivePort} ex={ex.Message}");

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

        _log?.Info($"[SDK.{nameof(ReliableClient)}] Disconnected (requested).");
        OnDisconnected?.Invoke(this, null);

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="System.ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the client is not connected.</exception>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(ReliableClient));
        FRAME_SENDER sender = System.Threading.Volatile.Read(ref _sender);

        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(payload, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="packet"/> is <c>null</c>.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the client is not connected.</exception>
    public System.Threading.Tasks.Task<System.Boolean> SendAsync(IPacket packet, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(ReliableClient));
        FRAME_SENDER sender = System.Threading.Volatile.Read(ref _sender);

        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(packet, ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Idempotent and safe to call multiple times. After disposal, the instance is no longer usable.
    /// </remarks>
    public void Dispose()
    {
        // Atomic flip: only the first caller proceeds.
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        CLEANUP_CONNECTION();

        _log?.Info($"[SDK.{nameof(ReliableClient)}] Disposed.");
        System.GC.SuppressFinalize(this);
    }

    #endregion Public API

    #region Private — Connection Lifecycle

    /// <summary>
    /// Starts the bandwidth rate sampler as a recurring task.
    /// If the <see cref="TaskManager"/> is unavailable, the sampler is silently skipped
    /// (non-critical telemetry).
    /// </summary>
    private void START_RATE_SAMPLER(
        System.Net.IPAddress addr,
        System.UInt16 port,
        System.Threading.CancellationToken loopToken)
    {
        try
        {
            _rateSamplerName = $"ReliableClient_Rate-{addr}:{port}";

            InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: _rateSamplerName,
                interval: System.TimeSpan.FromMilliseconds(1000),
                work: async (workerCt) =>
                {
                    System.Threading.CancellationToken effective =
                        workerCt.CanBeCanceled ? workerCt : loopToken;
                    await RATE_SAMPLER_TICK_ASYNC(effective).ConfigureAwait(false);
                },
                options: new RecurringOptions { NonReentrant = true, Tag = TaskNaming.Tags.Service }
            );
        }
        catch (System.Exception ex)
        {
            _log?.Warn(
                $"[SDK.{nameof(ReliableClient)}] schedule-rate-sampler-failed ex={ex.Message}");
            // Rate sampling is non-critical; skip without fallback.
        }
    }

    /// <summary>
    /// Schedules the receive loop as a <see cref="TaskManager"/> worker.
    /// If scheduling with the <see cref="TaskManager"/> fails, falls back to using
    /// <see cref="System.Threading.Tasks.Task.Run(System.Func{System.Threading.Tasks.Task}, System.Threading.CancellationToken)"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.Task"/> that represents the scheduled receive loop.
    /// </returns>
    private void START_RECEIVE_WORKER(
        System.Net.IPAddress addr,
        System.UInt16 port,
        System.Threading.CancellationToken loopToken)
    {
        try
        {
            _receiveHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"ReliableClient-Receive-{addr}:{port}",
                group: "client",
                work: async (_, workerCt) =>
                {
                    System.Threading.CancellationToken effective =
                        workerCt.CanBeCanceled ? workerCt : loopToken;
                    await _receiver.ReceiveLoopAsync(effective).ConfigureAwait(false);
                },
                options: new WorkerOptions { CancellationToken = loopToken, Tag = TaskNaming.Tags.Service }
            );
        }
        catch (System.Exception ex)
        {
            _log?.Warn(
                $"[SDK.{nameof(ReliableClient)}] schedule-receive-failed; falling back to Task.Run. ex={ex.Message}");

            // Receive is critical — must run even if TaskManager is unavailable.
            _ = System.Threading.Tasks.Task.Run(
                () => _receiver.ReceiveLoopAsync(loopToken),
                System.Threading.CancellationToken.None);
        }
    }

    /// <summary>
    /// Schedules the heartbeat (keep-alive) loop as a recurring task.
    /// Falls back to an internal <see cref="HEARTBEAT_LOOP_ASYNC"/> loop if scheduling fails.
    /// No-op when <see cref="TransportOptions.KeepAliveIntervalMillis"/> is zero.
    /// </summary>
    private void START_HEARTBEAT(
        System.Net.IPAddress addr,
        System.UInt16 port,
        System.Threading.CancellationToken loopToken)
    {
        if (Options.KeepAliveIntervalMillis <= 0)
        {
            return;
        }

        System.TimeSpan interval =
            System.TimeSpan.FromMilliseconds(System.Math.Max(1000, Options.KeepAliveIntervalMillis));

        try
        {
            _heartbeatName = $"ReliableClient-Heartbeat-{addr}:{port}";

            InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: _heartbeatName,
                interval: interval,
                work: async (workerCt) =>
                {
                    System.UInt32 sequenceId = Csprng.NextUInt32();
                    System.Threading.CancellationToken effective = workerCt.CanBeCanceled ? workerCt : loopToken;
                    try
                    {
                        // Build PING — dùng như ControlExtensions PingAsync nhưng không cần trả về tuple.
                        Control ping = this.NewControl(0, ControlType.PING).WithSeq(sequenceId)
                                                                           .StampNow()
                                                                           .Build();

                        System.Int64 sendMono = ping.MonoTicks != 0 ? ping.MonoTicks : Clock.MonoTicksNow();

                        await this.SendAsync(ping, effective)
                                  .ConfigureAwait(false);

                        var pong = await this.AwaitControlAsync(
                            predicate: c => c.Type == ControlType.PONG && c.SequenceId == sequenceId,
                            timeoutMs: Options.KeepAliveIntervalMillis / 2 > 3000 ? Options.KeepAliveIntervalMillis / 2 : 3000,
                            ct: effective).ConfigureAwait(false);

                        // Đo RTT
                        System.Int64 nowMono = Clock.MonoTicksNow();
                        System.Double rtt = pong.MonoTicks > 0 && pong.MonoTicks <= nowMono ? Clock.MonoTicksToMilliseconds(nowMono - pong.MonoTicks) : Clock.MonoTicksToMilliseconds(nowMono - sendMono);

                        // Ghi nhận kết quả vào field (bảo vệ thread-safe)
                        lock (_heartbeatLock)
                        {
                            _lastHeartbeatRtt = rtt;
                            _lastHeartbeatPong = pong;
                        }
                    }
                    catch (System.OperationCanceledException) when (effective.IsCancellationRequested)
                    {
                        // Cancellation là bình thường.
                    }
                    catch (System.TimeoutException)
                    {
                        // Nếu timeout không nhận được pong, nên xóa đi thông tin cũ cho an toàn.
                        lock (_heartbeatLock)
                        {
                            _lastHeartbeatRtt = 0;
                            _lastHeartbeatPong = null;
                        }
                        _log?.Warn($"[SDK.{nameof(ReliableClient)}] heartbeat PONG timeout.");
                    }
                    catch (System.Exception ex)
                    {
                        _log?.Warn($"[SDK.{nameof(ReliableClient)}] heartbeat-send-or-pong-error: {ex.Message}");

                        try { OnError?.Invoke(this, ex); } catch { }
                        _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
                    }
                },
                options: new RecurringOptions { NonReentrant = true, Tag = TaskNaming.Tags.Service }
            );
        }
        catch (System.Exception ex)
        {
            _log?.Warn($"[SDK.{nameof(ReliableClient)}] schedule-heartbeat-failed; falling back to loop. ex={ex.Message}");
            _ = System.Threading.Tasks.Task.Run(() => HEARTBEAT_LOOP_ASYNC(loopToken), System.Threading.CancellationToken.None);
        }
    }

    /// <summary>
    /// Cancels and disposes all active TaskManager handles and the socket.
    /// Safe to call from <see cref="Dispose"/> and <see cref="DisconnectAsync"/>.
    /// </summary>
    private void CLEANUP_CONNECTION()
    {
        lock (_sync)
        {
            CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);

            System.Threading.Interlocked.Exchange(ref _sender, null)?.Dispose();

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

            _socket = null;
        }

        // Cancel TaskManager handles (best-effort; individual failures must not abort cleanup).
        try
        {
            if (_heartbeatName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelRecurring(_heartbeatName);
                _heartbeatName = null;
            }

            if (_rateSamplerName is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelRecurring(_rateSamplerName);
                _rateSamplerName = null;
            }

            if (_receiveHandle is not null)
            {
                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_receiveHandle.Id);
                _receiveHandle = null;
            }
        }
        catch { /* best-effort; swallow */ }
    }

    /// <summary>
    /// Cancels and disposes a <see cref="System.Threading.CancellationTokenSource"/>, then sets it to <see langword="null"/>.
    /// Must be called from within <c>lock (_sync)</c>.
    /// </summary>
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

    #endregion Private — Connection Lifecycle

    #region Private — Callbacks

    private void ReportBytesSent(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesSent, count);
        System.Threading.Interlocked.Add(ref _sendCounterForInterval, count);

        try
        {
            OnBytesSent?.Invoke(this, count);
        }
        catch { /* swallow subscriber exceptions */ }
    }

    private void ReportBytesReceived(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesReceived, count);
        System.Threading.Interlocked.Add(ref _receiveCounterForInterval, count);

        try
        {
            OnBytesReceived?.Invoke(this, count);
        }
        catch { /* swallow subscriber exceptions */ }
    }

    private void HandleSendError(System.Exception ex)
    {
        try
        {
            OnError?.Invoke(this, ex);
        }
        catch { }

        _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
    }

    private void HandleReceiveError(System.Exception ex)
    {
        try
        {
            OnError?.Invoke(this, ex);
        }
        catch { }

        _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
    }

    private void HandleReceiveMessage(Nalix.Shared.Memory.Buffers.BufferLease lease)
    {
        // ── Snapshot invocation list ─────────────────────────────────────
        // GetInvocationList() trả về array snapshot tại thời điểm gọi.
        // An toàn với concurrent subscribe/unsubscribe vì delegate là immutable.
        System.Delegate[] syncHandlers = OnMessageReceived?.GetInvocationList() ?? [];
        System.Func<ReliableClient, System.ReadOnlyMemory<System.Byte>, System.Threading.Tasks.Task> asyncHandler = OnMessageReceivedAsync;

        // ── Chuẩn bị data cho async handler TRƯỚC KHI dispose lease ─────
        // ToArray() copy Span ra heap — không cần pool, không cần dispose.
        // Chỉ allocate nếu thực sự có asyncHandler để tránh alloc vô ích.
        System.ReadOnlyMemory<System.Byte> asyncData = asyncHandler is not null ? lease.Span.ToArray() : System.ReadOnlyMemory<System.Byte>.Empty;

        try
        {
            // ── Deliver tới từng sync subscriber ─────────────────────────
            // Mỗi subscriber nhận BufferLease COPY RIÊNG — sở hữu và dispose độc lập.
            // Wrapper trong ReliableClientSubscriptions đã có finally { buffer.Dispose(); }
            // nên copy sẽ được trả về pool đúng cách sau khi handler chạy xong.
            foreach (System.Delegate d in syncHandlers)
            {
                // CopyFrom: rent buffer mới từ pool + copy nội dung lease gốc vào.
                // Đây là zero-copy đối với lease gốc (chỉ đọc Span), không cần lock.
                Nalix.Shared.Memory.Buffers.BufferLease copy =
                    Nalix.Shared.Memory.Buffers.BufferLease.CopyFrom(lease.Span);

                try
                {
                    ((System.EventHandler<BufferLease>)d).Invoke(this, copy);
                }
                catch (System.Exception ex)
                {
                    // Subscriber faulted — wrapper của nó có thể đã không chạy finally.
                    // Dispose copy ngay tại đây để không leak pool buffer.
                    try { copy.Dispose(); } catch { }

                    _log?.Error($"[SDK.{nameof(ReliableClient)}] sync-handler-faulted: {ex.Message}", ex);
                }
            }
        }
        finally
        {
            // ── Dispose lease gốc — đúng 1 lần, đúng chỗ ───────────────
            // Dù có bao nhiêu subscriber, dù subscriber nào throw,
            // lease gốc luôn được dispose tại đây.
            try { lease.Dispose(); } catch { }
        }

        // ── Fire async handler ───────────────────────────────────────────
        // Nằm ngoài try/finally vì lease đã được dispose an toàn rồi.
        // asyncData là heap copy độc lập — không cần dispose.
        if (asyncHandler is not null && asyncData.Length > 0)
        {
            _ = asyncHandler(this, asyncData).ContinueWith(
                t => _log?.Error($"[SDK.{nameof(ReliableClient)}] OnMessageReceivedAsync faulted: {t.Exception?.GetBaseException().Message}"),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    #endregion Private — Callbacks

    #region Private — Background Loops

    /// <summary>
    /// Fallback heartbeat loop used when <see cref="TaskManager"/> scheduling fails.
    /// Sends a CONTROL PING at the configured interval until canceled.
    /// </summary>
    private async System.Threading.Tasks.Task HEARTBEAT_LOOP_ASYNC(System.Threading.CancellationToken token)
    {
        System.Int32 intervalMs = System.Math.Max(1, Options.KeepAliveIntervalMillis);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(intervalMs, token).ConfigureAwait(false);

                // Send CONTROL PING as heartbeat.
                await this.SendControlAsync(
                    opCode: 0,
                    type: ControlType.PING,
                    configure: ctrl =>
                    {
                        ctrl.SequenceId = Csprng.NextUInt32();
                        ctrl.Protocol = ProtocolType.TCP;
                        ctrl.MonoTicks = Clock.MonoTicksNow();
                        ctrl.Timestamp = Clock.UnixMillisecondsNow();
                    },
                    ct: token
                ).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                break; // Expected on disconnect/dispose.
            }
            catch (System.Exception ex)
            {
                _log?.Warn(
                    $"[SDK.{nameof(ReliableClient)}.{nameof(HEARTBEAT_LOOP_ASYNC)}] heartbeat-error: {ex.Message}");

                try { OnError?.Invoke(this, ex); } catch { }
                _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
                break;
            }
        }
    }

    /// <summary>
    /// Samples per-interval byte counters and updates the last BPS readings.
    /// Intentionally synchronous (no awaitable work); returned <see cref="System.Threading.Tasks.Task"/>
    /// is completed immediately.
    /// </summary>
    private System.Threading.Tasks.Task RATE_SAMPLER_TICK_ASYNC(System.Threading.CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        System.Int64 now = Clock.MonoTicksNow();
        System.Double elapsedSec = Clock.MonoTicksToMilliseconds(now - _lastSampleTick) / 1000.0;

        _lastSampleTick = now;

        try
        {
            System.Int64 sent = System.Threading.Interlocked.Exchange(ref _sendCounterForInterval, 0);
            System.Int64 recv = System.Threading.Interlocked.Exchange(ref _receiveCounterForInterval, 0);

            System.Threading.Interlocked.Exchange(ref _lastSendBps, (System.Int64)(sent / elapsedSec));
            System.Threading.Interlocked.Exchange(ref _lastReceiveBps, recv);
        }
        catch (System.Exception ex)
        {
            _log?.Warn($"[SDK.{nameof(ReliableClient)}.{nameof(RATE_SAMPLER_TICK_ASYNC)}] sampler-error: {ex.Message}");

            try
            {
                OnError?.Invoke(this, ex);
            }
            catch { }
        }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>
    /// Handles unexpected disconnections and drives the exponential-backoff reconnect loop.
    /// Fires <see cref="OnDisconnected"/> once, then retries <see cref="ConnectAsync"/>
    /// until successful, exhausted, or disposed.
    /// </summary>
    private async System.Threading.Tasks.Task HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(System.Exception cause)
    {
        try { OnDisconnected?.Invoke(this, cause); } catch { }

        // Tear down current socket AND cancel all background tasks before reconnect.
        // This prevents "duplicate recurring name" warnings on reconnect.
        CLEANUP_CONNECTION();

        // Tear down the current socket.
        lock (_sync)
        {
            CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            try { _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _socket?.Close(); _socket?.Dispose(); } catch { }
            _socket = null;
        }

        if (!Options.ReconnectEnabled || System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        if (System.String.IsNullOrEmpty(_host) || _port == 0)
        {
            _log?.Info(
                $"[SDK.{nameof(ReliableClient)}] No saved endpoint; skipping auto-reconnect.");
            return;
        }

        System.Int32 attempt = 0;
        // Use long for delay arithmetic; clamp before casting to int for Task.Delay.
        System.Int64 delayMs = System.Math.Max(1L, Options.ReconnectBaseDelayMillis);
        System.Int64 maxDelayMs = System.Math.Max(1L, Options.ReconnectMaxDelayMillis);

        while (System.Threading.Volatile.Read(ref _disposed) == 0 &&
               (Options.ReconnectMaxAttempts == 0 || attempt < Options.ReconnectMaxAttempts))
        {
            attempt++;

            _log?.Info(
                $"[SDK.{nameof(ReliableClient)}] Reconnect attempt={attempt} delay={delayMs} ms.");

            // Safe cast: delayMs is clamped to maxDelayMs which is derived from an int option.
            System.Int64 jitter = (System.Int64)(Csprng.NextDouble() * delayMs * 0.3);
            await System.Threading.Tasks.Task.Delay((System.Int32)System.Math
                                             .Min(delayMs + jitter, System.Int32.MaxValue))
                                             .ConfigureAwait(false);

            try
            {
                await ConnectAsync(_host, _port).ConfigureAwait(false);
                _log?.Info($"[SDK.{nameof(ReliableClient)}] Reconnect-success attempt={attempt}.");

                return;
            }
            catch (System.Exception ex)
            {
                _log?.Warn($"[SDK.{nameof(ReliableClient)}] Reconnect-failed attempt={attempt} ex={ex.Message}");

                try
                {
                    OnError?.Invoke(this, ex);
                }
                catch { }

                // Exponential backoff — doubles each attempt, capped at maxDelayMs.
                delayMs = System.Math.Min(maxDelayMs, delayMs * 2);
            }
        }

        _log?.Info($"[SDK.{nameof(ReliableClient)}] Reconnect attempts exhausted.");
    }

    #endregion Private — Background Loops

    #region Private — Helpers

    /// <summary>
    /// Returns the current socket if it is connected; otherwise throws <see cref="System.InvalidOperationException"/>.
    /// Used as the socket accessor delegate passed to <see cref="FRAME_SENDER"/> and <see cref="FRAME_READER"/>.
    /// </summary>
    private System.Net.Sockets.Socket GET_CONNECTED_SOCKET_OR_THROW()
    {
        System.Net.Sockets.Socket s = _socket;

        return s?.Connected == true
            ? s
            : throw new System.InvalidOperationException("Client not connected.");
    }

    #endregion Private — Helpers
}