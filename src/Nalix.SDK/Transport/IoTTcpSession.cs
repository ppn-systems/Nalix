// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a TCP session implementation optimized for IoT scenarios,
/// including thread-safe connect logic, event-driven frame handling,
/// simple bandwidth tracking, and automatic reconnection.
/// </summary>
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class IoTTcpSession : TcpSessionBase, IDisposable
{
    #region Fields

    private string? _host;
    private ushort? _port;
    internal long _bytesSent;
    private int _reconnecting;
    internal long _bytesReceived;
    private int _hasEverConnected;

    /// <summary>
    /// Serializes Connect/Disconnect operations — prevents concurrent reconnect races.
    /// Disposed in <see cref="Dispose"/>.
    /// </summary>
    private readonly SemaphoreSlim _connectLock = new(1, 1);

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

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="IoTTcpSession"/> using
    /// <see cref="ConfigurationManager"/> and <see cref="InstanceManager"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="IPacketRegistry"/> is not registered
    /// or <see cref="TransportOptions"/> fails validation.
    /// </exception>
    public IoTTcpSession()
    {
        Catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
            ?? throw new InvalidOperationException(
                $"[SDK.{nameof(IoTTcpSession)}] IPacketRegistry not found in InstanceManager.");

        Options = ConfigurationManager.Instance.Get<TransportOptions>();
        Options.Validate();

        Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Created, options validated.");
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IoTTcpSession"/> with explicit dependencies.
    /// </summary>
    /// <param name="options">Transport configuration. Must not be <see langword="null"/>.</param>
    /// <param name="registry">Packet registry. Must not be <see langword="null"/>.</param>
    public IoTTcpSession(TransportOptions options, IPacketRegistry registry)
    {
        Options = options;
        Catalog = registry;

        ArgumentNullException.ThrowIfNull(Options);
        ArgumentNullException.ThrowIfNull(Catalog);
    }

    #endregion Constructors

    #region Overrides

    /// <inheritdoc/>
    protected override void InitializeFrame()
    {
        i_sender = new FRAME_SENDER(RequireConnectedSocket, Options, ReportBytesSent, HandleSendError);
        i_receiver = new FRAME_READER(RequireConnectedSocket, Options, HandleReceiveMessage, HandleReceiveError, ReportBytesReceived);

        Logging?.Debug($"[SDK.{nameof(IoTTcpSession)}] Frame helpers created.");
    }

    /// <inheritdoc/>
    protected override void StartReceiveWorker(CancellationToken loopToken)
    {
        if (i_receiver is null)
        {
            return;
        }

        // IoT target: no TaskManager dependency — plain Task.Run is intentional here.
        // The task is fire-and-forget; errors are routed through HANDLE_RECEIVE_ERROR.
        _ = Task.Run(async () =>
        {
            try
            {
                Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Receive worker started.");
                await i_receiver.ReceiveLoopAsync(loopToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — do not log as error.
            }
            catch (Exception ex)
            {
                Logging?.Error($"[SDK.{nameof(IoTTcpSession)}] Receive worker crashed: {ex.Message}", ex);
                HandleReceiveError(ex);
            }
        }, CancellationToken.None);
    }

    /// <inheritdoc/>
    public override async Task ConnectAsync(
        string? host = null,
        ushort? port = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) == 1, nameof(IoTTcpSession));

        // Serialize concurrent connect calls — important for IoT auto-reconnect racing.
        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string? effectiveHost = string.IsNullOrWhiteSpace(host) ? Options.Address : host;
            ushort effectivePort = port ?? Options.Port;

            if (string.IsNullOrWhiteSpace(effectiveHost))
            {
                throw new ArgumentException("Host is required.", nameof(host));
            }

            // Already connected to the same endpoint — nothing to do.
            if (IsConnected &&
                string.Equals(_host, effectiveHost, StringComparison.OrdinalIgnoreCase) &&
                _port == effectivePort)
            {
                Logging?.Debug($"[SDK.{nameof(IoTTcpSession)}] Already connected to {effectiveHost}:{effectivePort}.");
                return;
            }

            if (IsConnected)
            {
                Logging?.Debug($"[SDK.{nameof(IoTTcpSession)}] Cleaning up existing connection.");
                TearDownConnection();
            }

            lock (i_sync)
            {
                if (i_loopCts is not null)
                {
                    CancelAndDispose(ref i_loopCts);
                }
            }

            SetState(TcpSessionState.Connecting);

            // IoT networks are often slow — always apply a connect timeout.
            int timeoutMs = Options.ConnectTimeoutMillis > 0
                ? Options.ConnectTimeoutMillis
                : 15_000; // 15 s default for IoT

            using CancellationTokenSource connectCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(timeoutMs);

            // Fast path: skip DNS if already an IP address.
            IPAddress[] addrs = IPAddress.TryParse(effectiveHost, out IPAddress? ip)
                ? [ip]
                : await Dns.GetHostAddressesAsync(effectiveHost, connectCts.Token)
                                      .ConfigureAwait(false);

            Exception? lastEx = null;

            foreach (IPAddress addr in addrs)
            {
                Socket s = new(
                    addr.AddressFamily,
                    SocketType.Stream,
                    ProtocolType.Tcp);

                try
                {
                    s.NoDelay = Options.NoDelay;
                    s.SendBufferSize = Options.BufferSize;
                    s.ReceiveBufferSize = Options.BufferSize;

                    await s.ConnectAsync(
                        new IPEndPoint(addr, effectivePort),
                        connectCts.Token).ConfigureAwait(false);

                    CancellationToken loopToken;

                    lock (i_sync)
                    {
                        i_socket = s;
                        i_loopCts = new CancellationTokenSource();
                        loopToken = i_loopCts.Token;
                        _host = effectiveHost;
                        _port = effectivePort;
                    }

                    InitializeFrame();

                    bool isReconnect =
                        Interlocked.Exchange(ref _hasEverConnected, 1) == 1;

                    if (isReconnect)
                    {
                        RaiseConnected();
                        RaiseReconnected(0);
                    }
                    else
                    {
                        Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Connected to {effectiveHost}:{effectivePort}.");
                        RaiseConnected();
                    }

                    StartReceiveWorker(loopToken);
                    _ = Interlocked.Exchange(ref _reconnecting, 0);

                    return;
                }
                catch (OperationCanceledException)
                {
                    try { s.Dispose(); } catch { }
                    Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] ConnectAsync cancelled for {addr}:{effectivePort}.");
                    throw;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    try { s.Dispose(); } catch { }
                    Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Failed to connect to {addr}:{effectivePort}: {ex.Message}", ex);
                }
            }

            Logging?.Error($"[SDK.{nameof(IoTTcpSession)}] Could not connect to {effectiveHost}:{effectivePort}; last error: {lastEx?.Message}");
            SetState(TcpSessionState.Disconnected);
            throw lastEx
                ?? new SocketException(
                    (int)SocketError.HostNotFound);
        }
        finally
        {
            _ = _connectLock.Release();
        }
    }

    /// <inheritdoc/>
    protected override void ReportBytesSent(int count)
    {
        _ = Interlocked.Add(ref _bytesSent, count);
        base.ReportBytesSent(count);
    }

    /// <inheritdoc/>
    protected override void ReportBytesReceived(int count)
    {
        _ = Interlocked.Add(ref _bytesReceived, count);
        base.ReportBytesReceived(count);
    }

    /// <inheritdoc/>
    protected override void HandleSendError(Exception ex)
    {
        Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Send error: {ex.Message}", ex);
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    /// <inheritdoc/>
    protected override void HandleReceiveError(Exception ex)
    {
        Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Receive error: {ex.Message}", ex);
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    /// <inheritdoc/>
    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "<Pending>")]
    protected override void TearDownConnection()
    {
        bool wasConnected = IsConnected;
        base.TearDownConnection();

        if (wasConnected)
        {
            Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Disconnected.");
            RaiseDisconnected(new Exception("Disconnected"));
        }
    }

    /// <inheritdoc/>
    public void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectLock.Dispose();
        }

        Dispose();
    }

    #endregion Overrides

    #region Private Methods

    /// <summary>
    /// Triggers a non-reentrant reconnect cycle after a send/receive error.
    /// Uses the same exponential backoff + jitter strategy as <see cref="TcpSession"/>.
    /// </summary>
    /// <param name="cause"></param>
    private void TriggerReconnect(Exception cause)
    {
        if (!Options.ReconnectEnabled)
        {
            return;
        }

        // CAS ensures only one reconnect loop runs at a time.
        if (Interlocked.CompareExchange(ref _reconnecting, 1, 0) != 0)
        {
            Logging?.Trace($"[SDK.{nameof(IoTTcpSession)}] Reconnect already in progress, skipping.");
            return;
        }

        Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Triggering auto-reconnect after: {cause.Message}");
        _ = ReconnectLoopAsync(cause);
    }

    private async Task ReconnectLoopAsync(Exception cause)
    {
        TearDownConnection();

        if (Volatile.Read(ref _disposed) == 1 ||
            string.IsNullOrEmpty(_host) || _port == 0)
        {
            _ = Interlocked.Exchange(ref _reconnecting, 0);
            return;
        }

        SetState(TcpSessionState.Reconnecting);

        int attempt = 0;
        long max = Math.Max(1, Options.ReconnectMaxDelayMillis);
        long delay = Math.Max(1, Options.ReconnectBaseDelayMillis);

        // Dedicated CTS so Dispose() can abort the delay immediately.
        using CancellationTokenSource reconnectCts = new();

        while (Volatile.Read(ref _disposed) == 0 &&
               (Options.ReconnectMaxAttempts == 0 || attempt < Options.ReconnectMaxAttempts))
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
                break; // Disposed during delay.
            }

            try
            {
                await ConnectAsync(_host, _port, reconnectCts.Token).ConfigureAwait(false);
                Logging?.Info($"[SDK.{nameof(IoTTcpSession)}] Reconnected after {attempt} attempt(s).");
                RaiseReconnected(attempt);
                return;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logging?.Warn($"[SDK.{nameof(IoTTcpSession)}] Reconnect attempt {attempt} failed: {ex.Message}", ex);
                delay = Math.Min(max, delay * 2);
            }
        }

        Logging?.Error($"[SDK.{nameof(IoTTcpSession)}] Reconnect exhausted after {attempt} attempt(s).");
        _ = Interlocked.Exchange(ref _reconnecting, 0);
        SetState(TcpSessionState.Disconnected);
    }

    #endregion Private Methods
}
