// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Configuration;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Extensions;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Represents a reliable TCP client session with automatic reconnection,
/// heartbeat support, and bandwidth monitoring.
/// </summary>
/// <remarks>
/// This class extends <see cref="TcpSessionBase"/> and delegates
/// framing, sending, and receiving logic to internal helpers.
/// </remarks>
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class TcpSession : TcpSessionBase
{
    #region Constants

    /// <summary>
    /// Gets the size of the packet header in bytes.
    /// </summary>
    public const byte HeaderSize = 2;

    #endregion Constants

    #region Fields

    private IWorkerHandle? _receiveHandle;
    private SessionMonitor? _monitor;
    private string? _host;
    private ushort? _port;
    private int _reconnecting;
    private int _hasEverConnected;

    private long _bytesSent;
    private long _lastSendBps;
    private long _bytesReceived;
    private long _lastReceiveBps;
    private long _sendCounterForInterval;
    private long _receiveCounterForInterval;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total number of bytes sent.
    /// </summary>
    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total number of bytes received.
    /// </summary>
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <summary>
    /// Gets the current send rate in bytes per second.
    /// </summary>
    public long SendBytesPerSecond => Interlocked.Read(ref _lastSendBps);

    /// <summary>
    /// Gets the current receive rate in bytes per second.
    /// </summary>
    public long ReceiveBytesPerSecond => Interlocked.Read(ref _lastReceiveBps);

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpSession"/> class.
    /// </summary>
    /// <exception cref="InternalErrorException">
    /// Thrown when required configuration or dependencies cannot be loaded.
    /// </exception>
    public TcpSession() : base()
    {
        try
        {
            this.Catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
                ?? throw new InvalidOperationException("IPacketRegistry instance not found.");

            this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
            this.Options.Validate();
            this.Logger?.Info($"[SDK.{this.GetType().Name}] TransportOptions loaded and validated");
        }
        catch (Exception ex)
        {
            throw new InternalErrorException($"[SDK.{this.GetType().Name}] Failed to load TransportOptions: {ex.Message}", ex);
        }

        if (this.Catalog is null)
        {
            throw new InternalErrorException($"[SDK.{this.GetType().Name}] Missing IPacketRegistry");
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpSession"/> class with an explicit packet registry
    /// and optional common services.
    /// </summary>
    /// <param name="registry">The packet registry used for packet serialization and deserialization.</param>
    /// <param name="logger">Optional logger override. When null, the logger is resolved from <see cref="InstanceManager"/>.</param>
    /// <param name="dispatcher">Optional dispatcher override. When null, the dispatcher is resolved from <see cref="InstanceManager"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
    public TcpSession(IPacketRegistry registry, ILogger? logger = null, IThreadDispatcher? dispatcher = null) : base(logger, dispatcher)
    {
        this.Catalog = registry ?? throw new ArgumentNullException(nameof(registry));
        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Options.Validate();
        this.Logger?.Info($"[SDK.{this.GetType().Name}] TransportOptions loaded and validated");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpSession"/> class.
    /// </summary>
    /// <param name="options">
    /// The transport configuration options used to initialize the TCP session.
    /// </param>
    /// <param name="registry">
    /// The packet registry responsible for managing and resolving packet handlers.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="registry"/> is null.
    /// </exception>
    public TcpSession(TransportOptions options, IPacketRegistry registry)
        : this(options, registry, logger: null, dispatcher: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpSession"/> class with explicit transport options,
    /// packet registry, and optional common services.
    /// </summary>
    /// <param name="options">The transport configuration options used to initialize the TCP session.</param>
    /// <param name="registry">The packet registry responsible for managing and resolving packet handlers.</param>
    /// <param name="logger">Optional logger override. When null, the logger is resolved from <see cref="InstanceManager"/>.</param>
    /// <param name="dispatcher">Optional dispatcher override. When null, the dispatcher is resolved from <see cref="InstanceManager"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="registry"/> is null.</exception>
    public TcpSession(
        TransportOptions options,
        IPacketRegistry registry,
        ILogger? logger = null,
        IThreadDispatcher? dispatcher = null)
        : base(logger, dispatcher)
    {
        this.Options = options;
        this.Catalog = registry;

        ArgumentNullException.ThrowIfNull(this.Options);
        ArgumentNullException.ThrowIfNull(this.Catalog);
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Connects to the specified TCP endpoint asynchronously.
    /// </summary>
    /// <param name="host">Target host name or IP address.</param>
    /// <param name="port">Target port number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when host is invalid.</exception>
    /// <exception cref="NetworkException">Thrown when connection fails.</exception>
    public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(TcpSession));

        string? effectiveHost = string.IsNullOrWhiteSpace(host) ? this.Options.Address : host;
        ushort effectivePort = port ?? this.Options.Port;

        if (string.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new ArgumentException("Host required");
        }

        if (this.IsConnected &&
            string.Equals(_host, effectiveHost, StringComparison.OrdinalIgnoreCase) &&
            _port == effectivePort)
        {
            this.Logger?.Debug($"[SDK.{this.GetType().Name}] Already connected to {effectiveHost}:{effectivePort}.");
            return;
        }

        if (this.IsConnected)
        {
            this.Logger?.Debug($"[SDK.{this.GetType().Name}] Cleaning up existing connection.");
            this.TearDownConnection();
        }

        lock (Sync)
        {
            if (LoopCts is not null)
            {
                CancelAndDispose(ref LoopCts);
            }
        }

        this.SetState(TcpSessionState.Connecting);

        using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (this.Options.ConnectTimeoutMillis > 0)
        {
            connectCts.CancelAfter(this.Options.ConnectTimeoutMillis);
        }

        Exception? lastEx = null;

        IPAddress[] addrs = await Dns.GetHostAddressesAsync(effectiveHost, connectCts.Token).ConfigureAwait(false);

        foreach (IPAddress addr in addrs)
        {
            Socket s = new(addr.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            try
            {
                s.NoDelay = this.Options.NoDelay;
                s.SendBufferSize = this.Options.BufferSize;
                s.ReceiveBufferSize = this.Options.BufferSize;

                await s.ConnectAsync(new IPEndPoint(addr, effectivePort), connectCts.Token).ConfigureAwait(false);

                CancellationToken loopToken;

                lock (Sync)
                {
                    Socket = s;
                    LoopCts = new CancellationTokenSource();
                    loopToken = LoopCts.Token;
                    _host = effectiveHost;
                    _port = effectivePort;
                }

                this.InitializeFrame();

                bool isReconnect = Interlocked.Exchange(ref _hasEverConnected, 1) == 1;
                if (isReconnect)
                {
                    this.RaiseConnected();
                    this.RaiseReconnected(0);
                }
                else
                {
                    this.Logger?.Info($"[SDK.{this.GetType().Name}] Connected to {effectiveHost}:{effectivePort}.");
                    this.RaiseConnected();
                }

                this.StartReceiveWorker(loopToken);
                _ = Interlocked.Exchange(ref _reconnecting, 0);

                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                this.Logger?.Warn($"[SDK.{this.GetType().Name}] Failed to connect to {addr}:{effectivePort}: {ex.Message}", ex);
                try { s.Dispose(); } catch { }
            }
            finally
            {
                s = null!;
            }
        }

        this.SetState(TcpSessionState.Disconnected);
        throw new NetworkException($"[SDK.{this.GetType().Name}] Could not connect to {effectiveHost}:{effectivePort}; last error: {lastEx?.Message}", lastEx ?? new SocketException((int)SocketError.HostNotFound));
    }

    #endregion APIs

    #region Overrides

    /// <summary>
    /// Creates internal frame sender and receiver helpers.
    /// </summary>
    protected override void InitializeFrame()
    {
        Sender = new FRAME_SENDER(this.RequireConnectedSocket, this.Options, this.ReportBytesSent, this.HandleSendError, this.Logger);
        Receiver = new FRAME_READER(this.RequireConnectedSocket, this.Options, this.HandleReceiveMessage, this.HandleReceiveError, this.ReportBytesReceived, this.Logger);

        this.Logger?.Debug($"[SDK.{this.GetType().Name}] Frame helpers created");
    }

    /// <summary>
    /// Starts the background worker responsible for receiving data.
    /// </summary>
    /// <param name="loopToken">Cancellation token controlling the receive loop.</param>
    protected override void StartReceiveWorker(CancellationToken loopToken)
    {
        if (Receiver is null)
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
                    CancellationToken effective = workerCt.CanBeCanceled ? workerCt : loopToken;
                    this.Logger?.Info($"[SDK.{this.GetType().Name}] Receive worker started");
                    await Receiver.ReceiveLoopAsync(effective).ConfigureAwait(false);
                },
                options: new WorkerOptions { CancellationToken = loopToken }
            );
        }
        catch (Exception ex)
        {
            this.Logger?.Warn($"[SDK.{this.GetType().Name}] Failed to schedule receive worker: {ex.Message}, falling back to Task.Run", ex);
            _ = Task.Run(() => Receiver.ReceiveLoopAsync(loopToken), loopToken);
        }

        // Start monitor (rate sampler + heartbeat) after receive worker is up.
        _monitor = new SessionMonitor(this, loopToken);
    }

    /// <inheritdoc/>
    protected override void ReportBytesSent(int count)
    {
        _ = Interlocked.Add(ref _bytesSent, count);
        _ = Interlocked.Add(ref _sendCounterForInterval, count);
        base.ReportBytesSent(count);
    }

    /// <inheritdoc/>
    protected override void ReportBytesReceived(int count)
    {
        _ = Interlocked.Add(ref _bytesReceived, count);
        _ = Interlocked.Add(ref _receiveCounterForInterval, count);
        base.ReportBytesReceived(count);
    }

    /// <inheritdoc/>
    protected override void HandleSendError(Exception ex)
    {
        this.Logger?.Warn($"[SDK.{this.GetType().Name}] Send error: {ex.Message}", ex);
        this.RaiseError(ex);
        this.TriggerReconnect(ex);
    }

    /// <inheritdoc/>
    protected override void HandleReceiveError(Exception ex)
    {
        this.Logger?.Warn($"[SDK.{this.GetType().Name}] Receive error: {ex.Message}", ex);
        this.RaiseError(ex);
        this.TriggerReconnect(ex);
    }

    private void TriggerReconnect(Exception ex)
    {
        if (Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0)
        {
            this.Logger?.Info($"[SDK.{this.GetType().Name}] Triggering auto-reconnect after error: {ex.Message}");
            _ = this.HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
        }
        else
        {
            this.Logger?.Trace($"[SDK.{this.GetType().Name}] Reconnect already in progress, skipping.");
        }
    }

    /// <inheritdoc/>
    protected override void TearDownConnection()
    {
        bool wasConnected = this.IsConnected;
        base.TearDownConnection();

        try
        {
            if (_receiveHandle != null)
            {
                try
                {
                    InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_receiveHandle.Id);

                    _receiveHandle = null;
                    this.Logger?.Debug($"[SDK.{this.GetType().Name}] Receive worker cancelled");
                }
                catch
                {
                    this.Logger?.Warn($"[SDK.{this.GetType().Name}] Failed to cancel receive worker for {_host}:{_port}");
                }
            }

            // Stop monitor when connection tears down.
            _monitor?.Stop();
            _monitor = null;
        }
        catch (Exception ex)
        {
            this.Logger?.Warn($"[SDK.{this.GetType().Name}] Exception during TearDownConnection: {ex.Message}", ex);
        }

        if (wasConnected)
        {
            this.Logger?.Info($"[SDK.{this.GetType().Name}] Disconnected");
            this.RaiseDisconnected(new SocketException((int)SocketError.NotConnected));
        }
    }

    #endregion Overrides

    #region Private 

    private async Task HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(Exception cause)
    {
        this.Logger?.Debug($"[SDK.{this.GetType().Name}] ReconnectAsync triggered after: {cause.Message}");
        this.TearDownConnection();

        if (!this.Options.ReconnectEnabled || Volatile.Read(ref _disposed) == 1)
        {
            _ = Interlocked.Exchange(ref _reconnecting, 0);
            return;
        }

        if (string.IsNullOrEmpty(_host) || _port == 0)
        {
            _ = Interlocked.Exchange(ref _reconnecting, 0);
            return;
        }

        this.SetState(TcpSessionState.Reconnecting);

        int attempt = 0;
        long max = Math.Max(1, this.Options.ReconnectMaxDelayMillis);
        long delay = Math.Max(1, this.Options.ReconnectBaseDelayMillis);

        // Use a dedicated CTS so Dispose() can cancel the delay immediately.
        using CancellationTokenSource reconnectCts = new();

        while (Volatile.Read(ref _disposed) == 0
           && (this.Options.ReconnectMaxAttempts == 0
           || attempt < this.Options.ReconnectMaxAttempts))
        {
            attempt++;
            long jitter = (long)(Csprng.NextDouble() * delay * 0.3);

            try
            {
                await Task.Delay(
                    (int)Math.Min(delay + jitter, int.MaxValue),
                    reconnectCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break; // Disposed during delay — exit immediately.
            }

            try
            {
                await this.ConnectAsync(_host, _port, reconnectCts.Token).ConfigureAwait(false);
                this.Logger?.Info($"[SDK.{this.GetType().Name}] Reconnected to {_host}:{_port} after {attempt} attempt(s).");
                this.RaiseReconnected(attempt);
                return;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                this.Logger?.Warn($"[SDK.{this.GetType().Name}] Reconnect attempt {attempt} failed: {ex.Message}", ex);
                delay = Math.Min(max, delay * 2);
            }
        }

        this.Logger?.Error($"[SDK.{this.GetType().Name}] Reconnect exhausted after {attempt} attempt(s).");
        _ = Interlocked.Exchange(ref _reconnecting, 0);
        this.SetState(TcpSessionState.Disconnected);
    }

    /// <summary>
    /// Manages rate sampling and heartbeat loops for a <see cref="TcpSession"/>.
    /// Encapsulates all monitoring concerns so they do not leak into extension methods.
    /// </summary>
    private sealed class SessionMonitor
    {
        private readonly TcpSession _session;
        private readonly CancellationTokenSource _cts;

        private IWorkerHandle? _samplerHandle;
        private IWorkerHandle? _heartbeatHandle;

        /// <summary>
        /// Monotonic tick captured at the last sample — stored entirely inside this class.
        /// </summary>
        private long _lastSampleTick;

        internal SessionMonitor(TcpSession session, CancellationToken linkedToken)
        {
            _session = session;
            _lastSampleTick = Clock.MonoTicksNow();

            // Link to the session's loop token so both loops stop on disconnect/dispose.
            _cts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);

            TaskManager taskManager = InstanceManager.Instance.GetOrCreateInstance<TaskManager>();

            _samplerHandle = this.ScheduleOrFallback(
                taskManager,
                name: $"TcpSession-RateSampler-{session._host}:{session._port}",
                work: (_, ct) => this.RateSamplerLoopAsync(ct),
                token: _cts.Token);

            _heartbeatHandle = this.ScheduleOrFallback(
                taskManager,
                name: $"TcpSession-Heartbeat-{session._host}:{session._port}",
                work: (_, ct) => this.HeartbeatLoopAsync(ct),
                token: _cts.Token);
        }

        /// <summary>Stops both background loops and cancels their workers.</summary>
        internal void Stop()
        {
            TaskManager? taskManager = null;
            try { taskManager = InstanceManager.Instance.GetOrCreateInstance<TaskManager>(); } catch { }

            CancelWorker(taskManager, ref _samplerHandle);
            CancelWorker(taskManager, ref _heartbeatHandle);

            try { _cts.Cancel(); } catch { }
            _cts.Dispose();
        }

        private IWorkerHandle? ScheduleOrFallback(
            TaskManager taskManager, string name,
            Func<IWorkerContext, CancellationToken, ValueTask> work, CancellationToken token)
        {
            try
            {
                return taskManager.ScheduleWorker(
                    name: name,
                    group: "client-monitor",
                    work: work,
                    options: new WorkerOptions { CancellationToken = token });
            }
            catch (Exception ex)
            {
                _session.Logger?.Warn($"[SDK.SessionMonitor] Failed to schedule '{name}' via TaskManager, falling back to Task.Run: {ex.Message}");
                _ = Task.Run(async () => await work(new InlineWorkerContext(name), token).ConfigureAwait(false), token);
                return null;
            }
        }

        private static void CancelWorker(TaskManager? taskManager, ref IWorkerHandle? handle)
        {
            if (handle is null)
            {
                return;
            }

            taskManager?.CancelWorker(handle.Id);
            handle = null;
        }

        // ── Rate sampler ─────────────────────────────────────────────────────

        /// <summary>
        /// Samples byte counters at each interval and updates the last BPS readings.
        /// </summary>
        /// <param name="ct"></param>
        private async ValueTask RateSamplerLoopAsync(CancellationToken ct)
        {
            // Use half the keep-alive interval, minimum 1 s, as sample cadence.
            int intervalMs = Math.Max(1_000, _session.Options.KeepAliveIntervalMillis / 2);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMs, ct).ConfigureAwait(false);
                    this.SampleOnce();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _session.Logger?.Warn($"[SDK.{nameof(TcpSession)}.{nameof(RateSamplerLoopAsync)}] sampler-error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Performs a single BPS sample. Extracted so it can be unit-tested independently.
        /// </summary>
        private void SampleOnce()
        {
            long now = Clock.MonoTicksNow();
            long elapsed = now - _lastSampleTick;
            _lastSampleTick = now;

            // Guard against zero or negative elapsed (clock skew, first tick).
            double elapsedSec = elapsed > 0
                ? Clock.MonoTicksToMilliseconds(elapsed) / 1_000.0
                : 1.0; // fallback: treat as 1 s to avoid divide-by-zero or Infinity

            long sent = Interlocked.Exchange(ref _session._sendCounterForInterval, 0);
            long recv = Interlocked.Exchange(ref _session._receiveCounterForInterval, 0);

            _ = Interlocked.Exchange(ref _session._lastSendBps, (long)(sent / elapsedSec));
            _ = Interlocked.Exchange(ref _session._lastReceiveBps, (long)(recv / elapsedSec));
        }

        // ── Heartbeat ────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a PING control frame at the configured keep-alive interval until cancellation.
        /// </summary>
        /// <param name="ct"></param>
        private async ValueTask HeartbeatLoopAsync(CancellationToken ct)
        {
            int intervalMs = _session.Options.KeepAliveIntervalMillis;

            // KeepAliveIntervalMillis = 0 nghĩa là heartbeat bị disabled.
            if (intervalMs <= 0)
            {
                _session.Logger?.Debug($"[SDK.{nameof(TcpSession)}.{nameof(HeartbeatLoopAsync)}] Heartbeat disabled (KeepAliveIntervalMillis=0).");
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMs, ct).ConfigureAwait(false);

                    await _session.SendControlAsync(
                        opCode: 0,
                        type: ControlType.PING,
                        configure: ctrl =>
                        {
                            ctrl.SequenceId = Csprng.NextUInt32();
                            ctrl.Protocol = Common.Networking.Protocols.ProtocolType.TCP;
                            ctrl.MonoTicks = Clock.MonoTicksNow();
                            ctrl.Timestamp = Clock.UnixMillisecondsNow();
                        },
                        ct: ct
                    ).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _session.Logger?.Warn($"[SDK.{nameof(TcpSession)}.{nameof(HeartbeatLoopAsync)}] heartbeat-error: {ex.Message}");
                    _ = _session.HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
                    break;
                }
            }
        }

        /// <summary>
        /// Minimal worker context used only by Task.Run fallbacks.
        /// Keeps fallback paths alive without depending on TaskManager internals.
        /// </summary>
        private sealed class InlineWorkerContext(string name) : IWorkerContext
        {
            public ISnowflake Id => Snowflake.Empty;
            public string Name => name;
            public string Group => "inline";
            public bool IsCancellationRequested => false;
            public void Beat() { }
            public void Advance(long delta, string? note = null) { }
        }
    }

    #endregion Private 
}
